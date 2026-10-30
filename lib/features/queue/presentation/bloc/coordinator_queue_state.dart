part of 'coordinator_queue_cubit.dart';

enum CoordinatorQueueStatus { loading, ready, error }

class CoordinatorQueueState extends Equatable {
  final CoordinatorQueueStatus status;

  /// Every job the API's queue lists, whatever its stage.
  final List<InspectionRequest> requests;

  /// The header tile the list is narrowed to, or `null` for every job.
  final QueueFilter? filter;
  final RequestsFailureCode? failure;

  const CoordinatorQueueState({
    this.status = CoordinatorQueueStatus.loading,
    this.requests = const [],
    this.filter,
    this.failure,
  });

  /// Every job still on the coordinator's radar: waiting on an inspector,
  /// already assigned/running, or approved and waiting to be sent to the
  /// client — narrowed to [filter] when one is set, soonest due date first.
  List<InspectionRequest> get jobs {
    final visible = requests
        .where((r) => (r.isReadyToAssign || r.isAssignedOrLater || r.isAwaitingSend) && (filter?.matches(r) ?? true))
        .toList()
      ..sort((a, b) => _dueKey(a).compareTo(_dueKey(b)));
    return visible;
  }

  /// What a header tile counts — always across every job, whichever filter
  /// is on, so the tiles stay a summary of the whole queue.
  int countOf(QueueFilter filter) => requests.where(filter.matches).length;

  static final DateTime _farFuture = DateTime(9999);

  static DateTime _dueKey(InspectionRequest r) =>
      r.scheduledAt ?? r.preferredDate ?? _farFuture;

  CoordinatorQueueState copyWith({
    CoordinatorQueueStatus? status,
    List<InspectionRequest>? requests,
    QueueFilter? filter,
    bool clearFilter = false,
    RequestsFailureCode? failure,
    bool clearFailure = false,
  }) {
    return CoordinatorQueueState(
      status: status ?? this.status,
      requests: requests ?? this.requests,
      filter: clearFilter ? null : (filter ?? this.filter),
      failure: clearFailure ? null : (failure ?? this.failure),
    );
  }

  @override
  List<Object?> get props => [status, requests, filter, failure];
}
