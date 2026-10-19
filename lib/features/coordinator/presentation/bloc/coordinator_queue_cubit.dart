import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../auth/domain/entities/user.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/domain/usecases/get_requests.dart';
import '../../domain/queue_filter.dart';
import '../../domain/usecases/get_inspectors.dart';

part 'coordinator_queue_state.dart';

/// Backs the coordinator's "Ready to assign" queue (Feature 04 §5): every
/// job that's either waiting on an inspector or already scheduled/running,
/// plus the inspector roster (just to show an assigned job's inspector
/// name — the assign screen owns the actual picking).
class CoordinatorQueueCubit extends Cubit<CoordinatorQueueState> {
  final GetRequests _getRequests;
  final GetInspectors _getInspectors;

  StreamSubscription<List<InspectionRequest>>? _requestsSubscription;
  StreamSubscription<List<AppUser>>? _inspectorsSubscription;

  CoordinatorQueueCubit(this._getRequests, this._getInspectors)
    : super(const CoordinatorQueueState());

  void start() {
    if (_requestsSubscription != null) return;
    _requestsSubscription = _getRequests().listen(
      (requests) => emit(
        state.copyWith(
          status: CoordinatorQueueStatus.ready,
          requests: requests,
          clearFailure: true,
        ),
      ),
      onError: (Object error) => emit(
        state.copyWith(
          status: CoordinatorQueueStatus.error,
          failure: error is RequestsFailure ? error.code : RequestsFailureCode.unknown,
        ),
      ),
    );

    // Supplementary — an assigned card just shows a blank inspector name
    // until this arrives, rather than blocking the queue.
    _inspectorsSubscription = _getInspectors().listen(
      (inspectors) => emit(state.copyWith(inspectors: inspectors)),
      onError: (_) {},
    );
  }

  /// Narrows the list to [filter]'s group; tapping the group that's already
  /// on turns the filter off and shows every job again.
  void toggleFilter(QueueFilter filter) => emit(
    state.filter == filter ? state.copyWith(clearFilter: true) : state.copyWith(filter: filter),
  );

  Future<void> retry() async {
    await _requestsSubscription?.cancel();
    _requestsSubscription = null;
    emit(state.copyWith(status: CoordinatorQueueStatus.loading, clearFailure: true));
    start();
  }

  @override
  Future<void> close() async {
    await _requestsSubscription?.cancel();
    await _inspectorsSubscription?.cancel();
    return super.close();
  }
}
