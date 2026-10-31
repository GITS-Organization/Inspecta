part of 'inspector_tasks_cubit.dart';

enum InspectorTasksStatus { loading, ready, error }

/// The Tasks tab's three lists: what's due today, what's coming up, and what
/// was due on an earlier day and still isn't done.
enum TasksFilter { today, upcoming, overdue }

class InspectorTasksState extends Equatable {
  final InspectorTasksStatus status;
  final List<InspectionRequest> requests;
  final TasksFilter filter;
  final RequestsFailureCode? failure;
  const InspectorTasksState({
    this.status = InspectorTasksStatus.loading,
    this.requests = const [],
    this.filter = TasksFilter.today,
    this.failure,
  });

  static DateTime _dateOnly(DateTime d) => DateTime(d.year, d.month, d.day);

  /// This job is still open work for the inspector — not yet certified.
  static bool _isOpen(InspectionRequest r) =>
      r.status == JobStatus.assigned || r.status == JobStatus.taskAccepted || r.status == JobStatus.inProgress;

  /// Which list [task] belongs in as of [now] (default: right now), by the
  /// day it's scheduled for — or null when it isn't open work, or has no
  /// day. A job scheduled earlier today is still today's, however late.
  static TasksFilter? filterOf(InspectionRequest task, {DateTime? now}) {
    final scheduledAt = task.scheduledAt;
    if (!_isOpen(task) || scheduledAt == null) return null;
    final today = _dateOnly(now ?? DateTime.now());
    final day = _dateOnly(scheduledAt);
    if (day.isBefore(today)) return TasksFilter.overdue;
    return day == today ? TasksFilter.today : TasksFilter.upcoming;
  }

  /// How many whole days past its day an overdue [task] is — 1 for a job that
  /// was due yesterday. Zero for anything not overdue.
  static int daysOverdue(InspectionRequest task, {DateTime? now}) {
    final scheduledAt = task.scheduledAt;
    if (filterOf(task, now: now) != TasksFilter.overdue || scheduledAt == null) return 0;
    final today = now ?? DateTime.now();
    return DateTime.utc(today.year, today.month, today.day)
        .difference(DateTime.utc(scheduledAt.year, scheduledAt.month, scheduledAt.day))
        .inDays;
  }

  /// The open jobs in [filter]'s list, earliest first: the longest overdue,
  /// today's by time of day, or the soonest to come.
  List<InspectionRequest> tasksFor(TasksFilter filter, {DateTime? now}) {
    return requests.where((r) => filterOf(r, now: now) == filter).toList()
      ..sort((a, b) => a.scheduledAt!.compareTo(b.scheduledAt!));
  }

  int countFor(TasksFilter filter, {DateTime? now}) => requests.where((r) => filterOf(r, now: now) == filter).length;

  /// The jobs in the list the inspector is looking at.
  List<InspectionRequest> get visibleTasks => tasksFor(filter);

  InspectorTasksState copyWith({
    InspectorTasksStatus? status,
    List<InspectionRequest>? requests,
    TasksFilter? filter,
    RequestsFailureCode? failure,
    bool clearFailure = false,
  }) {
    return InspectorTasksState(
      status: status ?? this.status,
      requests: requests ?? this.requests,
      filter: filter ?? this.filter,
      failure: clearFailure ? null : (failure ?? this.failure),
    );
  }

  @override
  List<Object?> get props => [
    status,
    requests,
    filter,
    failure,
  ];
}
