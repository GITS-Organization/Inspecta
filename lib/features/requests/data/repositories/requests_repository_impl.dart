import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/base/paginated.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/error/api_error_model.dart';
import '../../../../core/error/api_failure.dart';
import '../../domain/entities/inspection_request.dart';
import '../../domain/entities/request_item.dart';
import '../../domain/entities/request_triage.dart';
import '../../domain/entities/requests_failure.dart';
import '../../domain/repositories/requests_repository.dart';
import '../datasources/requests_api_datasource.dart';
import '../datasources/requests_remote_datasource.dart';
import '../models/inspection_request_model.dart';

class RequestsRepositoryImpl implements RequestsRepository {
  final RequestsRemoteDataSource _remote;
  final RequestsApiDataSource _api;

  const RequestsRepositoryImpl(this._remote, this._api);

  @override
  Future<Paginated<InspectionRequest>> listRequests({
    required RequestTriage triage,
    String? query,
    int page = 1,
    int pageSize = 20,
  }) async {
    final result = await _api.getRequests(
      triage: triage,
      query: query,
      page: page,
      pageSize: pageSize,
    );
    final models = result.fold((data) => data, (f) => throw _mapError(f));
    return Paginated.page(
      items: _entities(models),
      page: page,
      pageSize: pageSize,
      // Rows with an unknown status are dropped, which mustn't end the paging.
      rawCount: models.length,
    );
  }

  @override
  Future<InspectionRequest> confirmRequest(String requestId) async {
    final result = await _api.confirmRequest(requestId);
    final model = result.fold((data) => data, (f) => throw _mapError(f));
    final entity = model.toEntity();
    if (entity == null) {
      debugPrint('Confirmed request ${model.id} has an unknown status');
      throw const RequestsFailure(RequestsFailureCode.unknown);
    }
    return entity;
  }

  @override
  Future<void> deleteRequest(String requestId) async {
    final result = await _api.deleteRequest(requestId);
    result.fold((_) {}, (f) => throw _mapError(f));
  }

  @override
  Stream<List<InspectionRequest>> watchRequests() {
    return _remote
        .watchRequests()
        .map(_entities)
        .handleError((Object error) => throw _mapError(error));
  }

  @override
  Stream<List<InspectionRequest>> watchAssignedRequests(String inspectorId) {
    return _remote
        .watchAssignedRequests(inspectorId)
        .map(_entities)
        .handleError((Object error) => throw _mapError(error));
  }

  @override
  Future<InspectionRequest?> getById(String id) async {
    try {
      final model = await _remote.getById(id);
      return model?.toEntity();
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> updateStatus(
    String requestId,
    JobStatus status, {
    String? note,
  }) async {
    try {
      await _remote.updateStatus(requestId, status, note: note);
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> markCertificateSubmitted(String requestId) async {
    try {
      await _remote.markCertificateSubmitted(requestId);
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> rejectRequest({
    required String requestId,
    required String reason,
  }) async {
    try {
      await _remote.updateStatus(
        requestId,
        JobStatus.quoteRejected,
        note: reason,
        extra: {'rejectReason': reason},
      );
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> assignInspector({
    required String requestId,
    required String inspectorId,
    required DateTime scheduledAt,
    String? note,
  }) async {
    try {
      await _remote.assignInspector(
        requestId,
        inspectorId: inspectorId,
        scheduledAt: scheduledAt,
        note: note,
      );
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> declineAssignment({required String requestId, String? reason}) async {
    try {
      await _remote.declineAssignment(requestId, reason: reason);
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> completeIntake({
    required InspectionRequest request,
    required String clientName,
    required String location,
    String? accessNotes,
    required List<RequestItem> items,
  }) async {
    final result = await _api.updateRequest(
      request.id,
      clientId: request.clientId,
      clientName: clientName,
      location: location,
      preferredDate: request.preferredDate,
      accessNotes: accessNotes,
      items: items,
      expectedUpdatedAt: request.updatedAt,
    );
    result.fold((_) {}, (f) => throw _mapError(f));
  }

  /// The entities of [models], leaving out any whose status the app doesn't
  /// understand so they can't show up mis-filed.
  List<InspectionRequest> _entities(List<InspectionRequestModel> models) {
    final requests = <InspectionRequest>[];
    for (final model in models) {
      final entity = model.toEntity();
      if (entity != null) {
        requests.add(entity);
      } else {
        debugPrint('Skipping request ${model.id}: unknown status');
      }
    }
    return requests;
  }

  RequestsFailure _mapError(Object error) {
    if (error is RequestsFailure) return error;
    if (error is ApiFailure) error = error.error;
    if (error is ApiErrorModel) {
      return RequestsFailure(switch (error.code) {
        401 || 403 => RequestsFailureCode.permissionDenied,
        409 || 412 => RequestsFailureCode.conflict,
        null => switch (error.dioException?.type) {
          DioExceptionType.connectionError ||
          DioExceptionType.connectionTimeout ||
          DioExceptionType.sendTimeout ||
          DioExceptionType.receiveTimeout ||
          DioExceptionType.unknown => RequestsFailureCode.network,
          _ => RequestsFailureCode.unknown,
        },
        _ => RequestsFailureCode.unknown,
      });
    }
    if (error is FirebaseException) {
      return RequestsFailure(switch (error.code) {
        'permission-denied' => RequestsFailureCode.permissionDenied,
        'unavailable' => RequestsFailureCode.network,
        _ => RequestsFailureCode.unknown,
      });
    }
    debugPrint('Unexpected requests error: $error');
    return const RequestsFailure(RequestsFailureCode.unknown);
  }
}
