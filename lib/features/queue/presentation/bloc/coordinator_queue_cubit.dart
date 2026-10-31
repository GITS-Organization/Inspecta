import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../domain/usecases/get_queue.dart';
import '../../domain/entities/queue_filter.dart';

part 'coordinator_queue_state.dart';

/// Backs the coordinator queue (Feature 04 §5): every job the API lists as
/// accepted — waiting on an inspector, scheduled/running, or approved and
/// waiting to be sent to the client.
class CoordinatorQueueCubit extends Cubit<CoordinatorQueueState> {
  final GetQueue _getQueue;

  /// Bumped per fetch so a slow, superseded response can't overwrite a newer
  /// one.
  int _fetch = 0;

  CoordinatorQueueCubit(this._getQueue) : super(const CoordinatorQueueState());

  /// Loads the queue — also the pull-to-refresh and the reload after
  /// returning from a screen that may have moved a job on. A failed reload
  /// keeps the list already on screen; only a first load shows the error.
  Future<void> start() async {
    final fetch = ++_fetch;
    try {
      // The whole queue, plus the two hand-over lists by name: the approved
      // jobs waiting to be delivered and the ones already delivered. The
      // delivered list is a courtesy, so its failure doesn't fail the queue.
      final lists = await Future.wait([
        _getQueue(),
        _getQueue(status: 'approved'),
        _getQueue(status: 'delivered').catchError((_) => <InspectionRequest>[]),
      ]);
      if (isClosed || fetch != _fetch) return;

      final approvedIds = {for (final r in lists[1]) r.id};
      emit(
        state.copyWith(
          status: CoordinatorQueueStatus.ready,
          // The approved list is authoritative for the jobs in it.
          requests: [...lists[0].where((r) => !approvedIds.contains(r.id)), ...lists[1]],
          delivered: lists[2],
          clearFailure: true,
        ),
      );
    } catch (error) {
      if (isClosed || fetch != _fetch) return;
      if (state.status == CoordinatorQueueStatus.ready) return;
      emit(
        state.copyWith(
          status: CoordinatorQueueStatus.error,
          failure: error is RequestsFailure ? error.code : RequestsFailureCode.unknown,
        ),
      );
    }
  }

  /// Narrows the list to [filter]'s group; tapping the group that's already
  /// on turns the filter off and shows every job again.
  void toggleFilter(QueueFilter filter) => emit(
    state.filter == filter ? state.copyWith(clearFilter: true) : state.copyWith(filter: filter),
  );

  Future<void> retry() {
    emit(state.copyWith(status: CoordinatorQueueStatus.loading, clearFailure: true));
    return start();
  }
}
