import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/error/api_error_model.dart';
import '../../../../core/error/api_failure.dart';
import '../../domain/entities/client_response_outcome.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_failure.dart';
import '../../domain/entities/quote_line_item.dart';
import '../../domain/entities/reply_type.dart';
import '../../domain/repositories/quotation_repository.dart';
import '../datasources/quotation_api_datasource.dart';
import '../datasources/quotation_remote_datasource.dart';
import '../models/quotation_model.dart';

class QuotationRepositoryImpl implements QuotationRepository {
  final QuotationRemoteDataSource _remote;
  final QuotationApiDataSource _api;

  const QuotationRepositoryImpl(this._remote, this._api);

  @override
  Stream<List<Quotation>> watchAll() {
    return _remote
        .watchAll()
        .map(_toEntities)
        .handleError((Object error) => throw _mapError(error));
  }

  @override
  Future<List<Quotation>> list({int page = 1, int pageSize = 50}) async {
    final result = await _api.getQuotations(page: page, pageSize: pageSize);
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
      if (entity == null) throw const QuotationFailure(QuotationFailureCode.unknown);
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
    int? validityDays,
    String? message,
    List<String>? notes,
    List<String>? scopeOfWork,
  }) async {
    if (type != ReplyType.reject) {
      // Offers and accepts are sent through the API, which records the quote
      // and moves the request on itself.
      final result = await _api.sendQuote(requestId, validForDays: validityDays);
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
