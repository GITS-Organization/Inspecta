import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/enums/job_status.dart';
import '../../../queue/domain/usecases/get_queue.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';

part 'inspector_tasks_state.dart';

/// Backs the inspector's Tasks tab (Feature 05): the Today / Upcoming /
/// Overdue lists and the task cards. The jobs come from the API's queue, which
/// gives an inspector only their own.
class InspectorTasksCubit extends Cubit<InspectorTasksState> {
  final String inspectorId;
  final GetQueue _getQueue;

  /// Bumped per fetch so a slow, superseded response can't overwrite a newer
  /// one.
  int _fetch = 0;

  InspectorTasksCubit({
    required this.inspectorId,
    required GetQueue getQueue,
  }) : _getQueue = getQueue,
       super(const InspectorTasksState());

  /// Loads the tasks — also the pull-to-refresh and the reload after an
  /// action. A failed reload keeps the list already on screen; only a first
  /// load shows the error.
  Future<void> start() async {
    final fetch = ++_fetch;
    try {
      final requests = await _getQueue();
      if (isClosed || fetch != _fetch) return;
      emit(state.copyWith(status: InspectorTasksStatus.ready, requests: requests, clearFailure: true));
    } catch (error) {
      if (isClosed || fetch != _fetch) return;
      if (state.status == InspectorTasksStatus.ready) return;
      emit(
        state.copyWith(
          status: InspectorTasksStatus.error,
          failure: error is RequestsFailure ? error.code : RequestsFailureCode.unknown,
        ),
      );
    }
  }

  Future<void> retry() {
    emit(state.copyWith(status: InspectorTasksStatus.loading, clearFailure: true));
    return start();
  }

  void selectFilter(TasksFilter filter) => emit(state.copyWith(filter: filter));
}
