import 'package:flutter/foundation.dart';

import '../../../../core/base/paginated.dart';
import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/error/api_error_handler.dart';
import '../../../../core/error/api_failure.dart';
import '../../domain/entities/inspection_request.dart';
import '../../domain/entities/request_item.dart';
import '../../domain/entities/request_triage.dart';
import '../../domain/entities/requests_failure.dart';
import '../../domain/repositories/requests_repository.dart';
import '../datasources/requests_api_client_services.dart';
import '../datasources/requests_remote_datasource.dart';
import '../models/inspection_request_model.dart';
import '../models/requests_bodies.dart';

class RequestsRepositoryImpl implements RequestsRepository {
  static const _safeApi = SafeApi();

  final RequestsRemoteDataSource _remote;
  final RequestsApiClientServices _api;

  const RequestsRepositoryImpl(this._remote, this._api);

  @override
  Future<Paginated<InspectionRequest>> listRequests({
    required RequestTriage triage,
    String? query,
    int page = 1,
    int pageSize = 20,
  }) async {
    final result = await _safeApi(() async {
      final rows = await _api.getRequests(triage.value, query, page, pageSize);
      return [
        for (final json in (rows as List).whereType<Map<String, dynamic>>())
          InspectionRequestModel.fromApiJson(json),
      ];
    });
    final models = result.fold((data) => data, (f) => throw _mapError(f));
    return Paginated.page(
      items: _entities(models),
      page: page,
      pageSize: pageSize,
      rawCount: models.length,
    );
  }

  @override
  Future<InspectionRequest> confirmRequest(String requestId) async {
    final result = await _safeApi(
      () async => InspectionRequestModel.fromApiJson(
        await _api.confirmRequest(requestId) as Map<String, dynamic>,
      ),
    );
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
    final result = await _safeApi<void>(() => _api.deleteRequest(requestId));
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
    final result = await _safeApi(
      () async => InspectionRequestModel.fromApiJson(
        await _api.getRequest(id) as Map<String, dynamic>,
      ),
    );
    final model = result.fold<InspectionRequestModel?>(
      (data) => data,
      (f) => f is ApiFailure && f.code == 404 ? null : throw _mapError(f),
    );
    return model?.toEntity();
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
    final result = await _safeApi<void>(
      () => _api.rejectRequest(requestId, RejectRequestBody(reason: reason)),
    );
    result.fold((_) {}, (f) => throw _mapError(f));
  }

  @override
  Future<void> assignInspector({
    required String requestId,
    required String inspectorId,
    required DateTime scheduledAt,
    String? note,
  }) async {
    final result = await _safeApi<void>(
      () => _api.assignInspector(
        requestId,
        AssignInspectorBody(
          inspectorId: inspectorId,
          scheduledAt: scheduledAt,
          note: note,
        ),
      ),
    );
    result.fold((_) {}, (f) => throw _mapError(f));
  }

  @override
  Future<void> declineAssignment({
    required String requestId,
    String? reason,
  }) async {
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
    final result = await _safeApi(
      () => _api.updateRequest(
        request.id,
        UpdateRequestBody(
          clientId: request.clientId,
          clientName: clientName,
          location: location,
          preferredDate: request.preferredDate,
          accessNotes: accessNotes,
          items: items,
          expectedUpdatedAt: request.updatedAt,
        ),
      ),
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

  /// The requests failure for [error]; [ApiErrorHandler] does the classifying.
  RequestsFailure _mapError(Object error) {
    if (error is RequestsFailure) return error;
    return RequestsFailure(switch (ApiErrorHandler.classify(error)) {
      ApiErrorKind.permissionDenied => RequestsFailureCode.permissionDenied,
      ApiErrorKind.conflict => RequestsFailureCode.conflict,
      ApiErrorKind.network => RequestsFailureCode.network,
      ApiErrorKind.unknown => RequestsFailureCode.unknown,
    });
  }
}
