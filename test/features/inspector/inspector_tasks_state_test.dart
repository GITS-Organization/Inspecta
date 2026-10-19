import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/inspector/presentation/bloc/inspector_tasks_cubit.dart';
import 'package:inspecta/features/requests/domain/entities/inspection_request.dart';

import '../requests/fake_requests_repository.dart';

// Wednesday 21 Oct 2026, 14:00.
final _now = DateTime(2026, 10, 21, 14);

InspectionRequest _task(String id, DateTime? at, {JobStatus status = JobStatus.taskAccepted}) =>
    sampleRequest(id: id, status: status, scheduledAt: at);

List<String> _ids(List<InspectionRequest> tasks) => tasks.map((t) => t.id).toList();

void main() {
  group('which list a task is in', () {
    TasksFilter? filterOf(DateTime? at, {JobStatus status = JobStatus.taskAccepted}) =>
        InspectorTasksState.filterOf(_task('t', at, status: status), now: _now);

    test('today, however early or late in the day', () {
      expect(filterOf(DateTime(2026, 10, 21, 0, 5)), TasksFilter.today);
      expect(filterOf(DateTime(2026, 10, 21, 9)), TasksFilter.today);
      expect(filterOf(DateTime(2026, 10, 21, 23, 59)), TasksFilter.today);
    });

    test('a job whose time earlier today has passed is still today\'s, not overdue', () {
      expect(filterOf(DateTime(2026, 10, 21, 8)), TasksFilter.today);
    });

    test('upcoming from tomorrow on', () {
      expect(filterOf(DateTime(2026, 10, 22, 0, 1)), TasksFilter.upcoming);
      expect(filterOf(DateTime(2026, 12, 1, 9)), TasksFilter.upcoming);
    });

    test('overdue from yesterday back', () {
      expect(filterOf(DateTime(2026, 10, 20, 23, 59)), TasksFilter.overdue);
      expect(filterOf(DateTime(2026, 9, 1, 9)), TasksFilter.overdue);
    });

    test('any open status counts: waiting on a response, accepted or started', () {
      for (final status in [JobStatus.assigned, JobStatus.taskAccepted, JobStatus.inProgress]) {
        expect(filterOf(DateTime(2026, 10, 21, 9), status: status), TasksFilter.today, reason: '$status');
      }
    });

    test('finished work, and a job with no day, are in none', () {
      expect(filterOf(DateTime(2026, 10, 21, 9), status: JobStatus.certificateSubmitted), isNull);
      expect(filterOf(DateTime(2026, 10, 21, 9), status: JobStatus.sentToClient), isNull);
      expect(filterOf(DateTime(2026, 10, 1, 9), status: JobStatus.certificateReturned), isNull);
      expect(filterOf(null), isNull);
    });
  });

  group('how overdue', () {
    int days(DateTime at) => InspectorTasksState.daysOverdue(_task('t', at), now: _now);

    test('counts whole calendar days, whatever the time of day', () {
      expect(days(DateTime(2026, 10, 20, 23, 59)), 1);
      expect(days(DateTime(2026, 10, 20, 0, 1)), 1);
      expect(days(DateTime(2026, 10, 14, 9)), 7);
    });

    test('is zero for anything that is not overdue', () {
      expect(days(DateTime(2026, 10, 21, 8)), 0);
      expect(days(DateTime(2026, 10, 25, 9)), 0);
    });
  });

  group('the lists', () {
    final state = InspectorTasksState(
      requests: [
        _task('today-pm', DateTime(2026, 10, 21, 15)),
        _task('later', DateTime(2026, 10, 30, 9)),
        _task('yesterday', DateTime(2026, 10, 20, 9)),
        _task('today-am', DateTime(2026, 10, 21, 9)),
        _task('tomorrow', DateTime(2026, 10, 22, 11)),
        _task('long-ago', DateTime(2026, 10, 5, 9)),
        _task('done', DateTime(2026, 10, 21, 10), status: JobStatus.certificateSubmitted),
      ],
    );

    test('today\'s run in time order', () {
      expect(_ids(state.tasksFor(TasksFilter.today, now: _now)), ['today-am', 'today-pm']);
    });

    test('upcoming starts with the soonest', () {
      expect(_ids(state.tasksFor(TasksFilter.upcoming, now: _now)), ['tomorrow', 'later']);
    });

    test('overdue starts with the one that has waited longest', () {
      expect(_ids(state.tasksFor(TasksFilter.overdue, now: _now)), ['long-ago', 'yesterday']);
    });

    test('count what each holds, leaving finished work out', () {
      expect(state.countFor(TasksFilter.today, now: _now), 2);
      expect(state.countFor(TasksFilter.upcoming, now: _now), 2);
      expect(state.countFor(TasksFilter.overdue, now: _now), 2);
    });

    test('start on Today, and show the list that is picked', () {
      expect(state.filter, TasksFilter.today);
      expect(state.copyWith(filter: TasksFilter.overdue).filter, TasksFilter.overdue);
    });
  });

  group('the "new task" nudge', () {
    test('is for the earliest job still waiting on a response, whichever list it is in', () {
      final state = InspectorTasksState(
        requests: [
          _task('accepted', DateTime(2026, 10, 1, 9)),
          _task('waiting-later', DateTime(2026, 10, 30, 9), status: JobStatus.assigned),
          _task('waiting-sooner', DateTime(2026, 10, 25, 9), status: JobStatus.assigned),
        ],
      );

      expect(state.firstNeedsResponse?.id, 'waiting-sooner');
    });

    test('is absent when everything has been responded to', () {
      expect(InspectorTasksState(requests: [_task('a', DateTime(2026, 10, 21, 9))]).firstNeedsResponse, isNull);
    });
  });
}
