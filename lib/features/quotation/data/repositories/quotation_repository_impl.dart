import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter/foundation.dart';

import '../../domain/entities/client_response_outcome.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_failure.dart';
import '../../domain/entities/quote_line_item.dart';
import '../../domain/entities/reply_type.dart';
import '../../domain/repositories/quotation_repository.dart';
import '../datasources/quotation_remote_datasource.dart';
import '../models/quotation_model.dart';

class QuotationRepositoryImpl implements QuotationRepository {
  final QuotationRemoteDataSource _remote;

  const QuotationRepositoryImpl(this._remote);

  @override
  Stream<List<Quotation>> watchAll() {
    return _remote
        .watchAll()
        .map(_toEntities)
        .handleError((Object error) => throw _mapError(error));
  }

  @override
  Stream<List<Quotation>> watchForRequest(String requestId) {
    return _remote
        .watchForRequest(requestId)
        .map(_toEntities)
        .handleError((Object error) => throw _mapError(error));
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
  }) async {
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

  @override
  Future<void> sendReminder(String quotationId) async {
    try {
      await _remote.sendReminder(quotationId);
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
