import 'package:flutter/foundation.dart';

import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/error/api_error_handler.dart';
import '../../../../core/error/api_failure.dart';
import '../../../auth/domain/entities/user.dart';
import '../../domain/entities/coordinator_failure.dart';
import '../../domain/repositories/coordinator_repository.dart';
import '../datasources/coordinator_api_client_services.dart';
import '../models/inspector_model.dart';

class CoordinatorRepositoryImpl implements CoordinatorRepository {
  static const _safeApi = SafeApi();

  final CoordinatorApiClientServices _api;

  const CoordinatorRepositoryImpl(this._api);

  @override
  Future<List<AppUser>> listInspectors({String? managerId}) async {
    final result = await _safeApi(() async {
      final rows = await _api.getInspectors(managerId);
      return [
        for (final json in (rows as List).whereType<Map<String, dynamic>>())
          InspectorModel.fromApiJson(json).toEntity(),
      ];
    });
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  CoordinatorFailure _mapError(Object error) {
    if (error is CoordinatorFailure) return error;
    if (error is ApiFailure) {
      return CoordinatorFailure(switch (ApiErrorHandler.classify(error)) {
        ApiErrorKind.permissionDenied => CoordinatorFailureCode.permissionDenied,
        ApiErrorKind.network => CoordinatorFailureCode.network,
        ApiErrorKind.conflict || ApiErrorKind.unknown => CoordinatorFailureCode.unknown,
      });
    }
    debugPrint('Unexpected coordinator error: $error');
    return const CoordinatorFailure(CoordinatorFailureCode.unknown);
  }
}
