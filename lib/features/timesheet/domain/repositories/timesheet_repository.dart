import '../entities/timesheet_entry.dart';

/// Methods emit / throw [TimesheetFailure] on expected errors.
abstract interface class TimesheetRepository {
  /// Every entry this inspector has logged, across all their jobs.
  Stream<List<TimesheetEntry>> watchEntries(String inspectorId);

  /// Creates or replaces the entry for [TimesheetEntry.requestId], and
  /// (re)submits it for a coordinator's review: whatever its status was,
  /// it's pending afterwards.
  Future<void> saveEntry(TimesheetEntry entry);

  /// The inspector's hourly rate in piastres — 0 until they set one. Their
  /// prices are calculated from it.
  Stream<int> watchHourlyRate(String inspectorId);

  /// Sets the inspector's hourly rate. It applies to entries saved from now
  /// on; entries already logged keep the rate they were saved with.
  Future<void> saveHourlyRate(String inspectorId, int hourlyRatePiastres);
}
