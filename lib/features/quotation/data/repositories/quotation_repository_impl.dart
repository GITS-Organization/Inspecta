import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/error/api_error_model.dart';
import '../../../../core/error/api_failure.dart';
import '../../../../core/utils/currency.dart';
import '../../domain/entities/client_response_outcome.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_failure.dart';
import '../../domain/entities/quote_email.dart';
import '../../domain/entities/quote_line_item.dart';
import '../../domain/entities/reply_type.dart';
import '../../domain/repositories/quotation_repository.dart';
import '../datasources/quotation_api_client_services.dart';
import '../models/quotation_bodies.dart';
import '../models/quotation_model.dart';
import '../models/quote_email_model.dart';

class QuotationRepositoryImpl implements QuotationRepository {
  static const _safeApi = SafeApi();

  final QuotationApiClientServices _api;

  const QuotationRepositoryImpl(this._api);

  /// The version an accept or send response carries, when it has one.
  static int? _versionOf(Object? response) => response is Map<String, dynamic>
      ? (response['version'] as num?)?.toInt()
      : null;

  @override
  Future<List<Quotation>> list({
    int page = 1,
    int pageSize = 50,
    bool includeSuperseded = false,
  }) async {
    final result = await _safeApi(() async {
      final rows = await _api.getQuotations(includeSuperseded, page, pageSize);
      return [
        for (final json in (rows as List).whereType<Map<String, dynamic>>())
          QuotationModel.fromApiListJson(json),
      ];
    });
    return _toEntities(result.fold((data) => data, (f) => throw _mapError(f)));
  }

  @override
  Future<int> send({
    required String requestId,
    required ReplyType type,
    String? quotationId,
    String currency = Currency.egp,
    int? validityDays,
    QuoteEmailEdits? email,
  }) async {
    if (type == ReplyType.reject) {
      // A rejection closes the request instead and records no quote: see
      // RequestsRepository.rejectRequest.
      throw ArgumentError.value(type, 'type', 'A rejection is not a quote');
    }
    if (quotationId != null && type == ReplyType.accept) {
      final result = await _safeApi(
        () async => _versionOf(
          await _api.acceptQuote(requestId, quotationId, QuoteEmailModel.toBody(email)),
        ),
        mapError: _emailFailureOf,
      );
      return result.fold((version) => version ?? 1, (f) => throw _mapError(f));
    }
    if (quotationId != null && type == ReplyType.offer) {
      final result = await _safeApi(
        () async => _versionOf(
          await _api.sendExistingQuote(requestId, quotationId, QuoteEmailModel.toBody(email)),
        ),
        mapError: _emailFailureOf,
      );
      return result.fold((version) => version ?? 1, (f) => throw _mapError(f));
    }
    final result = await _safeApi(
      () async => QuotationModel.fromApiJson(
        await _api.sendQuote(
          requestId,
          SendQuoteBody(validForDays: validityDays, currency: currency),
        ) as Map<String, dynamic>,
      ),
    );
    final quote = result.fold((data) => data, (f) => throw _mapError(f));
    return quote.version ?? 1;
  }

  /// The answers sending the email has of its own: 400 a wrong address
  /// (nothing was sent), 502 the mail server refused (nothing changed, so it
  /// can be retried). Anything else (null) is classified like every other
  /// call.
  static QuotationFailure? _emailFailureOf(Object error) {
    if (error is! DioException) return null;
    return switch (error.response?.statusCode) {
      400 => const QuotationFailure(QuotationFailureCode.invalidAddress),
      502 => const QuotationFailure(QuotationFailureCode.mailRefused),
      _ => null,
    };
  }

  @override
  Future<QuoteEmailPreview> getEmailPreview({
    required String requestId,
    required String quotationId,
    required bool accept,
  }) async {
    final result = await _safeApi(
      () async => QuoteEmailModel.fromJson(
        await _api.getQuoteEmail(requestId, quotationId, accept ? 'accept' : 'send') as Map<String, dynamic>,
      ),
    );
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<void> updateOffer({
    required String requestId,
    required String quotationId,
    required List<QuoteLineItem> items,
    String currency = Currency.egp,
    int? validityDays,
    List<String>? scopeOfWork,
    List<String>? notes,
    String? message,
  }) async {
    final result = await _safeApi<void>(
      () => _api.updateQuote(
        requestId,
        quotationId,
        UpdateQuoteBody(
          items: items,
          currency: currency,
          validForDays: validityDays,
          scopeOfWork: scopeOfWork,
          notes: notes,
          message: message,
        ),
      ),
    );
    result.fold((_) {}, (f) => throw _mapError(f));
  }

  @override
  Future<Uint8List> getPdf({
    required String requestId,
    required String quotationId,
  }) async {
    final result = await _safeApi(
      () async =>
          Uint8List.fromList(await _api.getQuotePdf(requestId, quotationId)),
    );
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<Quotation> sendNewPrice({
    required String requestId,
    required int totalPiastres,
    String currency = Currency.egp,
    int? validityDays,
  }) async {
    final result = await _safeApi(
      () async => QuotationModel.fromApiJson(
        await _api.sendQuote(
          requestId,
          SendQuoteBody(
            validForDays: validityDays,
            amountPiastres: totalPiastres,
            currency: currency,
          ),
        ) as Map<String, dynamic>,
      ),
    );
    final model = result.fold((data) => data, (f) => throw _mapError(f));
    final quotation = model.toEntity();
    if (quotation == null)
      throw const QuotationFailure(QuotationFailureCode.unknown);
    return quotation;
  }

  @override
  Future<void> recordClientResponse({
    required String quotationId,
    required String requestId,
    required ClientResponseOutcome outcome,
    int? clientPricePiastres,
    String? note,
  }) async {
    // The API has no endpoint for logging the client's answer to a quote
    // (accepting here emails the PDF instead), so there is nothing to call.
    debugPrint('Client reply not recorded: the API has no endpoint for it');
    throw const QuotationFailure(QuotationFailureCode.unknown);
  }

  List<Quotation> _toEntities(List<QuotationModel> models) {
    final quotations = <Quotation>[];
    for (final model in models) {
      final entity = model.toEntity();
      if (entity != null) {
        quotations.add(entity);
      } else {
        debugPrint('Skipping quotations/${model.id}: unknown type/status');
      }
    }
    return quotations;
  }

  QuotationFailure _mapError(Object error) {
    if (error is QuotationFailure) return error;
    if (error is ApiFailure) error = error.error;
    if (error is ApiErrorModel) {
      return QuotationFailure(switch (error.code) {
        401 || 403 => QuotationFailureCode.permissionDenied,
        null => switch (error.dioException?.type) {
          DioExceptionType.connectionError ||
          DioExceptionType.connectionTimeout ||
          DioExceptionType.sendTimeout ||
          DioExceptionType.receiveTimeout ||
          DioExceptionType.unknown => QuotationFailureCode.network,
          _ => QuotationFailureCode.unknown,
        },
        _ => QuotationFailureCode.unknown,
      });
    }
    debugPrint('Unexpected quotation error: $error');
    return const QuotationFailure(QuotationFailureCode.unknown);
  }
}
