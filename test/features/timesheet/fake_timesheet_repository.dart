import 'dart:async';

import 'package:inspecta/features/timesheet/domain/entities/timesheet_activity.dart';
import 'package:inspecta/features/timesheet/domain/entities/timesheet_entry.dart';
import 'package:inspecta/features/timesheet/domain/entities/timesheet_entry_status.dart';
import 'package:inspecta/features/timesheet/domain/entities/timesheet_expense.dart';
import 'package:inspecta/features/timesheet/domain/entities/timesheet_failure.dart';
import 'package:inspecta/features/timesheet/domain/repositories/timesheet_repository.dart';

/// [minutes] is shorthand for that much inspection time; pass [activities]
/// for a breakdown.
TimesheetEntry sampleEntry({
  String requestId = 'REQ-1',
  String inspectorId = 'insp-1',
  int minutes = 90,
  Map<TimesheetActivity, int>? activities,
  int pricePiastres = 450000,
  int hourlyRatePiastres = 0,
  Map<TimesheetExpense, int> expenses = const {},
  TimesheetEntryStatus status = TimesheetEntryStatus.pending,
  String? reviewNote,
  String? reviewerName,
  DateTime? reviewedAt,
}) {
  return TimesheetEntry(
    requestId: requestId,
    inspectorId: inspectorId,
    activities: activities ?? {if (minutes > 0) TimesheetActivity.inspection: minutes},
    pricePiastres: pricePiastres,
    hourlyRatePiastres: hourlyRatePiastres,
    expenses: expenses,
    status: status,
    reviewNote: reviewNote,
    reviewerName: reviewerName,
    reviewedAt: reviewedAt,
  );
}

class FakeTimesheetRepository implements TimesheetRepository {
  /// What `watchEntries` (one inspector's own entries) emits.
  final controller = StreamController<List<TimesheetEntry>>.broadcast();

  /// What `watchHourlyRate` emits.
  final rateController = StreamController<int>.broadcast();

  TimesheetFailure? rateFailure;
  final savedRates = <(String inspectorId, int hourlyRatePiastres)>[];

  TimesheetFailure? saveFailure;
  final saved = <TimesheetEntry>[];

  @override
  Stream<List<TimesheetEntry>> watchEntries(String inspectorId) => controller.stream;

  @override
  Stream<int> watchHourlyRate(String inspectorId) => rateController.stream;

  @override
  Future<void> saveHourlyRate(String inspectorId, int hourlyRatePiastres) async {
    if (rateFailure != null) throw rateFailure!;
    savedRates.add((inspectorId, hourlyRatePiastres));
  }

  @override
  Future<void> saveEntry(TimesheetEntry entry) async {
    if (saveFailure != null) throw saveFailure!;
    saved.add(entry);
  }
}
