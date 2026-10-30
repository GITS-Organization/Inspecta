import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/error/api_error_handler.dart';
import '../../../../core/error/api_failure.dart';
import '../../../auth/domain/entities/user.dart';
import '../../domain/entities/coordinator_failure.dart';
import '../../domain/repositories/coordinator_repository.dart';
import '../datasources/coordinator_api_client_services.dart';
import '../datasources/inspectors_remote_datasource.dart';
import '../models/inspector_model.dart';

class CoordinatorRepositoryImpl implements CoordinatorRepository {
  static const _safeApi = SafeApi();

  final InspectorsRemoteDataSource _remote;
  final CoordinatorApiClientServices _api;

  const CoordinatorRepositoryImpl(this._remote, this._api);

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

  @override
  Stream<List<AppUser>> watchInspectors() {
    return _remote
        .watchInspectors()
        .map((models) {
          final inspectors = <AppUser>[];
          for (final model in models) {
            final entity = model.toEntity();
            if (entity != null && entity.active) {
              inspectors.add(entity);
            } else {
              debugPrint('Skipping users/${model.id}: not an active inspector');
            }
          }
          return inspectors;
        })
        .handleError((Object error) {
          throw _mapError(error);
        });
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
    if (error is FirebaseException) {
      return CoordinatorFailure(switch (error.code) {
        'permission-denied' => CoordinatorFailureCode.permissionDenied,
        'unavailable' => CoordinatorFailureCode.network,
        _ => CoordinatorFailureCode.unknown,
      });
    }
    debugPrint('Unexpected coordinator error: $error');
    return const CoordinatorFailure(CoordinatorFailureCode.unknown);
  }
}
