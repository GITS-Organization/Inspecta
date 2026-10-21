import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter/foundation.dart';

import '../../domain/entities/timesheet_entry.dart';
import '../../domain/entities/timesheet_failure.dart';
import '../../domain/repositories/timesheet_repository.dart';
import '../datasources/timesheet_remote_datasource.dart';
import '../models/timesheet_entry_model.dart';

class TimesheetRepositoryImpl implements TimesheetRepository {
  final TimesheetRemoteDataSource _remote;

  const TimesheetRepositoryImpl(this._remote);

  @override
  Stream<List<TimesheetEntry>> watchEntries(String inspectorId) =>
      _entities(_remote.watchEntries(inspectorId));

  @override
  Stream<int> watchHourlyRate(String inspectorId) =>
      _remote.watchHourlyRate(inspectorId).handleError((Object error) {
        throw _mapError(error);
      });

  @override
  Future<void> saveHourlyRate(String inspectorId, int hourlyRatePiastres) =>
      _guard(() => _remote.saveHourlyRate(inspectorId, hourlyRatePiastres));

  Stream<List<TimesheetEntry>> _entities(Stream<List<TimesheetEntryModel>> models) {
    return models
        .map((list) => [for (final model in list) model.toEntity()])
        .handleError((Object error) {
          throw _mapError(error);
        });
  }

  @override
  Future<void> saveEntry(TimesheetEntry entry) => _guard(() => _remote.save(TimesheetEntryModel.fromEntity(entry)));

  Future<void> _guard(Future<void> Function() action) async {
    try {
      await action();
    } catch (e) {
      throw _mapError(e);
    }
  }

  TimesheetFailure _mapError(Object error) {
    if (error is TimesheetFailure) return error;
    if (error is FirebaseException) {
      return TimesheetFailure(switch (error.code) {
        'permission-denied' => TimesheetFailureCode.permissionDenied,
        'unavailable' => TimesheetFailureCode.network,
        _ => TimesheetFailureCode.unknown,
      });
    }
    debugPrint('Unexpected timesheet error: $error');
    return const TimesheetFailure(TimesheetFailureCode.unknown);
  }
}
