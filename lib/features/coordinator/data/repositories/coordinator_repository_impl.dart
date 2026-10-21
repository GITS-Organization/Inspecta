import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter/foundation.dart';

import '../../../auth/domain/entities/user.dart';
import '../../domain/entities/coordinator_failure.dart';
import '../../domain/repositories/coordinator_repository.dart';
import '../datasources/inspectors_remote_datasource.dart';

class CoordinatorRepositoryImpl implements CoordinatorRepository {
  final InspectorsRemoteDataSource _remote;

  const CoordinatorRepositoryImpl(this._remote);

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
