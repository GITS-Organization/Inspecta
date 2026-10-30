import 'dart:typed_data';

import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/error/api_error_model.dart';
import '../../../../core/error/api_failure.dart';
import '../../domain/entities/client_response_outcome.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_failure.dart';
import '../../domain/entities/quote_line_item.dart';
import '../../domain/entities/reply_type.dart';
import '../../domain/repositories/quotation_repository.dart';
import '../datasources/quotation_api_client_services.dart';
import '../datasources/quotation_remote_datasource.dart';
import '../models/quotation_bodies.dart';
import '../models/quotation_model.dart';

class QuotationRepositoryImpl implements QuotationRepository {
  static const _safeApi = SafeApi();

  final QuotationRemoteDataSource _remote;
  final QuotationApiClientServices _api;

  const QuotationRepositoryImpl(this._remote, this._api);

  /// The version an accept or send response carries, when it has one.
  static int? _versionOf(Object? response) => response is Map<String, dynamic>
      ? (response['version'] as num?)?.toInt()
      : null;

  @override
  Stream<List<Quotation>> watchAll() {
    return _remote
        .watchAll()
        .map(_toEntities)
        .handleError((Object error) => throw _mapError(error));
  }

  @override
  Future<List<Quotation>> list({int page = 1, int pageSize = 50}) async {
    final result = await _safeApi(() async {
      final rows = await _api.getQuotations(false, page, pageSize);
      return [
        for (final json in (rows as List).whereType<Map<String, dynamic>>())
          QuotationModel.fromApiListJson(json),
      ];
    });
    return _toEntities(result.fold((data) => data, (f) => throw _mapError(f)));
  }

  @override
  Future<Quotation> saveDraft({
    required String requestId,
    required String requestNumber,
    required String clientName,
    required String equipmentSummary,
    required ReplyType type,
    required List<QuoteLineItem> items,
    int? validityDays,
    String? message,
    List<String>? notes,
    List<String>? scopeOfWork,
  }) async {
    try {
      final model = await _remote.saveDraft(
        requestId: requestId,
        requestNumber: requestNumber,
        clientName: clientName,
        equipmentSummary: equipmentSummary,
        type: type,
        items: items,
        validityDays: validityDays,
        message: message,
        notes: notes,
        scopeOfWork: scopeOfWork,
      );
      final entity = model.toEntity();
      if (entity == null) {
        throw const QuotationFailure(QuotationFailureCode.unknown);
      }
      return entity;
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<int> send({
    required String requestId,
    required String requestNumber,
    required String clientName,
    required String equipmentSummary,
    required ReplyType type,
    required List<QuoteLineItem> items,
    String? quotationId,
    int? validityDays,
    String? message,
    List<String>? notes,
    List<String>? scopeOfWork,
  }) async {
    if (quotationId != null && type == ReplyType.accept) {
      final result = await _safeApi(
        () async => _versionOf(await _api.acceptQuote(requestId, quotationId)),
      );
      return result.fold((version) => version ?? 1, (f) => throw _mapError(f));
    }
    if (quotationId != null && type == ReplyType.offer) {
      final result = await _safeApi(
        () async =>
            _versionOf(await _api.sendExistingQuote(requestId, quotationId)),
      );
      return result.fold((version) => version ?? 1, (f) => throw _mapError(f));
    }
    if (type != ReplyType.reject) {
      final result = await _safeApi(
        () async => QuotationModel.fromApiJson(
          await _api.sendQuote(
            requestId,
            SendQuoteBody(validForDays: validityDays),
          ) as Map<String, dynamic>,
        ),
      );
      final quote = result.fold((data) => data, (f) => throw _mapError(f));
      return quote.version ?? 1;
    }
    try {
      return await _remote.send(
        requestId: requestId,
        requestNumber: requestNumber,
        clientName: clientName,
        equipmentSummary: equipmentSummary,
        type: type,
        items: items,
        validityDays: validityDays,
        message: message,
        notes: notes,
        scopeOfWork: scopeOfWork,
      );
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> updateOffer({
    required String requestId,
    required String quotationId,
    required List<QuoteLineItem> items,
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
    int? validityDays,
  }) async {
    final result = await _safeApi(
      () async => QuotationModel.fromApiJson(
        await _api.sendQuote(
          requestId,
          SendQuoteBody(
            validForDays: validityDays,
            amountPiastres: totalPiastres,
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
    try {
      await _remote.recordClientResponse(
        quotationId: quotationId,
        requestId: requestId,
        outcome: outcome,
        clientPricePiastres: clientPricePiastres,
        note: note,
      );
    } catch (e) {
      throw _mapError(e);
    }
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
    if (error is FirebaseException) {
      return QuotationFailure(switch (error.code) {
        'permission-denied' => QuotationFailureCode.permissionDenied,
        'unavailable' => QuotationFailureCode.network,
        _ => QuotationFailureCode.unknown,
      });
    }
    debugPrint('Unexpected quotation error: $error');
    return const QuotationFailure(QuotationFailureCode.unknown);
  }
}
