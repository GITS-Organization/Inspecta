part of 'coordinator_queue_cubit.dart';

enum CoordinatorQueueStatus { loading, ready, error }

class CoordinatorQueueState extends Equatable {
  final CoordinatorQueueStatus status;
  final List<InspectionRequest> requests;
  final List<AppUser> inspectors;

  /// The header tile the list is narrowed to, or `null` for every job.
  final QueueFilter? filter;
  final RequestsFailureCode? failure;

  const CoordinatorQueueState({
    this.status = CoordinatorQueueStatus.loading,
    this.requests = const [],
    this.inspectors = const [],
    this.filter,
    this.failure,
  });

  /// Every job still on the coordinator's radar: waiting on an inspector,
  /// already assigned/running, or approved and waiting to be sent to the
  /// client — narrowed to [filter] when one is set,
  /// soonest due date first.
  List<InspectionRequest> get jobs {
    final visible = requests
        .where((r) => (r.isReadyToAssign || r.isAssignedOrLater || r.isAwaitingSend) && (filter?.matches(r) ?? true))
        .toList()
      ..sort((a, b) => _dueKey(a).compareTo(_dueKey(b)));
    return visible;
  }

  /// Just the jobs waiting on an inspector — the "+ Assign a job" picker
  /// on an inspector's detail screen only offers these.
  List<InspectionRequest> get readyToAssign {
    final visible = requests.where((r) => r.isReadyToAssign).toList()
      ..sort((a, b) => _dueKey(a).compareTo(_dueKey(b)));
    return visible;
  }

  /// What a header tile counts — always across every job, whichever filter
  /// is on, so the tiles stay a summary of the whole queue.
  int countOf(QueueFilter filter) => requests.where(filter.matches).length;

  int get unassignedCount => countOf(QueueFilter.unassigned);

  int get scheduledCount => countOf(QueueFilter.scheduled);

  int get inProgressCount => countOf(QueueFilter.inProgress);

  int get toSendCount => countOf(QueueFilter.toSend);

  /// The assigned inspector's name for a job's card, or `null` while the
  /// roster is still loading (or the id no longer matches anyone).
  String? inspectorName(String? inspectorId) {
    if (inspectorId == null) return null;
    for (final inspector in inspectors) {
      if (inspector.id == inspectorId) return inspector.name;
    }
    return null;
  }

  static final DateTime _farFuture = DateTime(9999);

  static DateTime _dueKey(InspectionRequest r) =>
      r.scheduledAt ?? r.preferredDate ?? _farFuture;

  CoordinatorQueueState copyWith({
    CoordinatorQueueStatus? status,
    List<InspectionRequest>? requests,
    List<AppUser>? inspectors,
    QueueFilter? filter,
    bool clearFilter = false,
    RequestsFailureCode? failure,
    bool clearFailure = false,
  }) {
    return CoordinatorQueueState(
      status: status ?? this.status,
      requests: requests ?? this.requests,
      inspectors: inspectors ?? this.inspectors,
      filter: clearFilter ? null : (filter ?? this.filter),
      failure: clearFailure ? null : (failure ?? this.failure),
    );
  }

  @override
  List<Object?> get props => [status, requests, inspectors, filter, failure];
}
