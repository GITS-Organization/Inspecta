import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/domain/usecases/get_my_tasks.dart';
import '../../domain/entities/timesheet_activity.dart';
import '../../domain/entities/timesheet_entry.dart';
import '../../domain/entities/timesheet_expense.dart';
import '../../domain/entities/timesheet_failure.dart';
import '../../domain/entities/timesheet_row.dart';
import '../../domain/usecases/save_hourly_rate.dart';
import '../../domain/usecases/save_timesheet_entry.dart';
import '../../domain/usecases/watch_hourly_rate.dart';
import '../../domain/usecases/watch_timesheet.dart';

part 'timesheet_state.dart';

/// Backs the inspector's Timesheet tab: the jobs they've accepted, month
/// by month, each with the time they logged and the price worked out from
/// it at their hourly rate.
class TimesheetCubit extends Cubit<TimesheetState> {
  final String inspectorId;
  final GetMyTasks _getMyTasks;
  final WatchTimesheet _watchTimesheet;
  final WatchHourlyRate _watchHourlyRate;
  final SaveTimesheetEntry _saveEntry;
  final SaveHourlyRate _saveHourlyRate;

  StreamSubscription<List<InspectionRequest>>? _tasksSubscription;
  StreamSubscription<List<TimesheetEntry>>? _entriesSubscription;
  StreamSubscription<int>? _rateSubscription;

  // The tab is ready only once every stream has delivered, so it never
  // briefly shows every job as "not logged yet" while entries still load, or
  // prices a save against a rate that hasn't arrived.
  bool _hasTasks = false;
  bool _hasEntries = false;
  bool _hasRate = false;

  TimesheetCubit({
    required this.inspectorId,
    required GetMyTasks getMyTasks,
    required WatchTimesheet watchTimesheet,
    required WatchHourlyRate watchHourlyRate,
    required SaveTimesheetEntry saveEntry,
    required SaveHourlyRate saveHourlyRate,
  }) : _getMyTasks = getMyTasks,
       _watchTimesheet = watchTimesheet,
       _watchHourlyRate = watchHourlyRate,
       _saveEntry = saveEntry,
       _saveHourlyRate = saveHourlyRate,
       super(TimesheetState());

  void start() {
    if (_tasksSubscription != null) return;
    _tasksSubscription = _getMyTasks(inspectorId).listen((requests) {
      _hasTasks = true;
      emit(_withStatus(state.copyWith(requests: requests)));
    }, onError: _onError);
    _entriesSubscription = _watchTimesheet(inspectorId).listen((entries) {
      _hasEntries = true;
      emit(_withStatus(state.copyWith(entries: entries)));
    }, onError: _onError);
    _rateSubscription = _watchHourlyRate(inspectorId).listen((rate) {
      _hasRate = true;
      emit(_withStatus(state.copyWith(hourlyRatePiastres: rate)));
    }, onError: _onError);
  }

  TimesheetState _withStatus(TimesheetState next) => next.copyWith(
    status: _hasTasks && _hasEntries && _hasRate ? TimesheetStatus.ready : null,
    clearFailure: true,
  );

  /// A Firestore stream ends for good once it errors, so the others are
  /// stopped too — otherwise the tab would flip back to "ready" on a
  /// survivor's next event and show jobs with some of the data missing.
  void _onError(Object error) {
    _cancelSubscriptions();
    emit(state.copyWith(status: TimesheetStatus.error, failure: _failureCode(error)));
  }

  static TimesheetFailureCode _failureCode(Object error) {
    if (error is TimesheetFailure) return error.code;
    if (error is RequestsFailure) {
      return switch (error.code) {
        RequestsFailureCode.permissionDenied => TimesheetFailureCode.permissionDenied,
        RequestsFailureCode.network => TimesheetFailureCode.network,
        RequestsFailureCode.conflict || RequestsFailureCode.unknown => TimesheetFailureCode.unknown,
      };
    }
    return TimesheetFailureCode.unknown;
  }

  Future<void> retry() async {
    await _cancelSubscriptions();
    emit(state.copyWith(status: TimesheetStatus.loading, clearFailure: true));
    start();
  }

  Future<void> _cancelSubscriptions() async {
    final subscriptions = <StreamSubscription<Object?>?>[
      _tasksSubscription,
      _entriesSubscription,
      _rateSubscription,
    ];
    _tasksSubscription = null;
    _entriesSubscription = null;
    _rateSubscription = null;
    _hasTasks = false;
    _hasEntries = false;
    _hasRate = false;
    await Future.wait([for (final s in subscriptions) ?s?.cancel()]);
  }

  void previousMonth() => _shiftMonth(-1);

  void nextMonth() => _shiftMonth(1);

  void _shiftMonth(int delta) {
    final month = state.selectedMonth;
    emit(state.copyWith(selectedMonth: DateTime(month.year, month.month + delta)));
  }

  /// Logs (or replaces) the time and costs for a job. The price is not an
  /// input: it's worked out here, from the net working time and the
  /// inspector's current hourly rate, and the rate is saved with the entry
  /// so a later change of rate leaves it alone. Returns `null` on success,
  /// or the reason it failed so the caller can show it.
  Future<TimesheetFailureCode?> saveEntry({
    required String requestId,
    required Map<TimesheetActivity, int> activities,
    required Map<TimesheetExpense, int> expenses,
  }) async {
    try {
      await _saveEntry(
        TimesheetEntry.priced(
          requestId: requestId,
          inspectorId: inspectorId,
          activities: activities,
          expenses: expenses,
          hourlyRatePiastres: state.hourlyRatePiastres,
        ),
      );
      return null;
    } catch (e) {
      return _failureCode(e);
    }
  }

  /// Sets the inspector's hourly rate. It prices entries saved from now on;
  /// ones already logged keep the rate they were saved with.
  Future<TimesheetFailureCode?> saveHourlyRate(int hourlyRatePiastres) async {
    try {
      await _saveHourlyRate(inspectorId, hourlyRatePiastres);
      return null;
    } catch (e) {
      return _failureCode(e);
    }
  }

  @override
  Future<void> close() async {
    await _cancelSubscriptions();
    return super.close();
  }
}
