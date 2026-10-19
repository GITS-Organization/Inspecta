import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/enums/job_status.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/domain/usecases/accept_task.dart';
import '../../../requests/domain/usecases/decline_task.dart';
import '../../../requests/domain/usecases/get_my_tasks.dart';
import '../../../requests/domain/usecases/start_inspection.dart';

part 'inspector_tasks_state.dart';

/// Backs the inspector's Tasks tab (Feature 05): the Today / Upcoming /
/// Overdue lists, the "needs response" banner and the task cards — plus
/// accepting, declining and starting a job.
class InspectorTasksCubit extends Cubit<InspectorTasksState> {
  final String inspectorId;
  final GetMyTasks _getMyTasks;
  final AcceptTask _acceptTask;
  final DeclineTask _declineTask;
  final StartInspection _startInspection;

  StreamSubscription<List<InspectionRequest>>? _subscription;

  InspectorTasksCubit({
    required this.inspectorId,
    required GetMyTasks getMyTasks,
    required AcceptTask acceptTask,
    required DeclineTask declineTask,
    required StartInspection startInspection,
  }) : _getMyTasks = getMyTasks,
       _acceptTask = acceptTask,
       _declineTask = declineTask,
       _startInspection = startInspection,
       super(const InspectorTasksState());

  void start() {
    if (_subscription != null) return;
    _subscription = _getMyTasks(inspectorId).listen(
      (requests) => emit(
        state.copyWith(status: InspectorTasksStatus.ready, requests: requests, clearFailure: true),
      ),
      onError: (Object error) => emit(
        state.copyWith(
          status: InspectorTasksStatus.error,
          failure: error is RequestsFailure ? error.code : RequestsFailureCode.unknown,
        ),
      ),
    );
  }

  Future<void> retry() async {
    await _subscription?.cancel();
    _subscription = null;
    emit(state.copyWith(status: InspectorTasksStatus.loading, clearFailure: true));
    start();
  }

  void selectFilter(TasksFilter filter) => emit(state.copyWith(filter: filter));

  Future<void> accept(String requestId) =>
      _run(InspectorTaskAction.accept, () => _acceptTask(requestId));

  Future<void> decline(String requestId, {String? reason}) =>
      _run(InspectorTaskAction.decline, () => _declineTask(requestId: requestId, reason: reason));

  Future<void> startInspection(String requestId) =>
      _run(InspectorTaskAction.start, () => _startInspection(requestId));

  Future<void> _run(InspectorTaskAction kind, Future<void> Function() action) async {
    if (state.isSubmitting) return;
    emit(state.copyWith(isSubmitting: true));
    try {
      await action();
      _complete(kind, success: true);
    } catch (e) {
      _complete(kind, success: false, failure: e is RequestsFailure ? e.code : RequestsFailureCode.unknown);
    }
  }

  void _complete(InspectorTaskAction kind, {required bool success, RequestsFailureCode? failure}) {
    if (isClosed) return;
    emit(
      state.copyWith(
        isSubmitting: false,
        actionSeq: state.actionSeq + 1,
        lastAction: kind,
        lastActionSuccess: success,
        lastActionFailure: failure,
        clearActionFailure: failure == null,
      ),
    );
  }

  @override
  Future<void> close() async {
    await _subscription?.cancel();
    return super.close();
  }
}
