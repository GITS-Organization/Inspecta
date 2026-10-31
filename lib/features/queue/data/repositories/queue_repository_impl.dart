import 'package:flutter/foundation.dart';

import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/error/api_error_handler.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../domain/repositories/queue_repository.dart';
import '../datasources/queue_api_client_services.dart';
import '../models/queue_item_model.dart';

class QueueRepositoryImpl implements QueueRepository {
  static const _safeApi = SafeApi();

  final QueueApiClientServices _api;

  const QueueRepositoryImpl(this._api);

  @override
  Future<List<InspectionRequest>> list({
    String? managerId,
    String? status,
    String? query,
    int page = 1,
    int pageSize = 50,
  }) async {
    return _load(() => _api.getQueue(managerId, status, query, page, pageSize));
  }

  @override
  Future<List<InspectionRequest>> toReview({
    String? managerId,
    String? inspectorId,
    int page = 1,
    int pageSize = 50,
  }) {
    return _load(() => _api.getToReview(managerId, inspectorId, page, pageSize));
  }

  Future<List<InspectionRequest>> _load(Future<dynamic> Function() fetch) async {
    final result = await _safeApi(() async {
      final rows = await fetch();
      return [
        for (final json in (rows as List).whereType<Map<String, dynamic>>())
          QueueItemModel.fromApiJson(json),
      ];
    });
    final models = result.fold((data) => data, (f) => throw _mapError(f));

    final requests = <InspectionRequest>[];
    for (final model in models) {
      final entity = model.toEntity();
      if (entity != null) {
        requests.add(entity);
      } else {
        debugPrint('Skipping queue row ${model.requestId}: unknown status');
      }
    }
    return requests;
  }

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
