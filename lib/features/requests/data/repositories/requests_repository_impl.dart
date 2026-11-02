import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/base/paginated.dart';
import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/error/api_error_handler.dart';
import '../../../../core/error/api_failure.dart';
import '../../domain/entities/inspection_request.dart';
import '../../domain/entities/machine_suggestion.dart';
import '../../domain/entities/request_item.dart';
import '../../domain/entities/request_triage.dart';
import '../../domain/entities/requests_failure.dart';
import '../../domain/repositories/requests_repository.dart';
import '../datasources/requests_api_client_services.dart';
import '../models/inspection_request_model.dart';
import '../models/requests_bodies.dart';

class RequestsRepositoryImpl implements RequestsRepository {
  static const _safeApi = SafeApi();

  final RequestsApiClientServices _api;

  const RequestsRepositoryImpl(this._api);

  @override
  Future<Paginated<InspectionRequest>> listRequests({
    required RequestTriage triage,
    String? query,
    int page = 1,
    int pageSize = 20,
  }) async {
    final result = await _safeApi(() async {
      final rows = await _api.getRequests(
        triage.value,
        triage.status?.value,
        query,
        page,
        pageSize,
      );
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
      mapError: _clientRequiredOf,
    );
    final model = result.fold((data) => data, (f) => throw _mapError(f));
    final entity = model.toEntity();
    if (entity == null) {
      debugPrint('Confirmed request ${model.id} has an unknown status');
      throw const RequestsFailure(RequestsFailureCode.unknown);
    }
    return entity;
  }

  /// The 409 `client_required` answer to a confirm: the sender isn't
  /// registered under a client yet.
  static ClientRequiredFailure? _clientRequiredOf(Object error) {
    if (error is! DioException || error.response?.statusCode != 409) return null;
    final data = error.response?.data;
    return data is Map && data['code'] == 'client_required' ? const ClientRequiredFailure() : null;
  }

  /// The 400 answer to saving items: a machine of another client, or one
  /// that doesn't exist. Nothing was saved.
  static InvalidMachineFailure? _invalidMachineOf(Object error) =>
      error is DioException && error.response?.statusCode == 400 ? const InvalidMachineFailure() : null;

  @override
  Future<List<MachineSuggestion>> getSuggestions(String requestId) async {
    final result = await _safeApi(() async {
      final rows = await _api.getSuggestions(requestId);
      return [
        for (final json in (rows is List ? rows : const []).whereType<Map<String, dynamic>>())
          if (json['equipmentId'] is String)
            MachineSuggestion(
              equipmentId: json['equipmentId'] as String,
              name: json['name'] as String? ?? '',
              quantity: (json['quantity'] as num?)?.toInt() ?? 1,
              confidence: (json['confidence'] as num?)?.toDouble() ?? 0,
              level: SuggestionLevel.fromValue(json['level']),
              reason: json['reason'] as String?,
              evidence: json['evidence'] as String?,
            ),
      ];
    });
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<InspectionRequest> autofill(String requestId) async {
    final result = await _safeApi(
      () async => InspectionRequestModel.fromApiJson(
        await _api.autofillRequest(requestId) as Map<String, dynamic>,
      ),
    );
    final model = result.fold((data) => data, (f) => throw _mapError(f));
    final entity = model.toEntity();
    if (entity == null) throw const RequestsFailure(RequestsFailureCode.unknown);
    return entity;
  }

  @override
  Future<void> deleteRequest(String requestId) async {
    final result = await _safeApi<void>(() => _api.deleteRequest(requestId));
    result.fold((_) {}, (f) => throw _mapError(f));
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
  Future<void> completeIntake({
    required InspectionRequest request,
    required String clientName,
    required String location,
    String? clientId,
    String? accessNotes,
    required List<RequestItem> items,
    bool prepareQuotation = false,
  }) async {
    final result = await _safeApi(
      () => _api.updateRequest(
        request.id,
        UpdateRequestBody(
          clientId: clientId ?? request.clientId,
          clientName: clientName,
          location: location,
          preferredDate: request.preferredDate,
          accessNotes: accessNotes,
          items: items,
          expectedUpdatedAt: request.updatedAt,
          prepareQuotation: prepareQuotation,
        ),
      ),
      mapError: _invalidMachineOf,
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
