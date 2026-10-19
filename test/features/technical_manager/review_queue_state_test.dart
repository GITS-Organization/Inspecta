import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/requests/domain/entities/inspection_request.dart';
import 'package:inspecta/features/technical_manager/presentation/bloc/review_queue_cubit.dart';

import '../requests/fake_requests_repository.dart';

InspectionRequest _job(
  String id, {
  JobStatus status = JobStatus.certificateSubmitted,
  DateTime? submitted,
  DateTime? scheduled,
  DateTime? received,
}) => sampleRequest(
  id: id,
  status: status,
  certificateSubmittedAt: submitted,
  scheduledAt: scheduled,
  receivedAt: received ?? DateTime(2026, 9, 1),
);

List<String> _ids(List<InspectionRequest> jobs) => jobs.map((j) => j.id).toList();

void main() {
  group('ReviewQueueState', () {
    test('lists the most recently submitted certificate first', () {
      final state = ReviewQueueState(
        requests: [
          _job('oldest', submitted: DateTime(2026, 10, 1, 9)),
          _job('newest', submitted: DateTime(2026, 10, 3, 9)),
          _job('middle', submitted: DateTime(2026, 10, 2, 9)),
        ],
      );

      expect(_ids(state.toReview), ['newest', 'middle', 'oldest']);
    });

    test('goes by when it was submitted, not by when the job was scheduled', () {
      final state = ReviewQueueState(
        requests: [
          // Scheduled last, but handed in first.
          _job('late-slot', scheduled: DateTime(2026, 10, 9), submitted: DateTime(2026, 10, 1)),
          _job('early-slot', scheduled: DateTime(2026, 10, 2), submitted: DateTime(2026, 10, 3)),
        ],
      );

      expect(_ids(state.toReview), ['early-slot', 'late-slot']);
    });

    test('a resubmitted certificate moves back to the top', () {
      final jobs = [
        _job('a', submitted: DateTime(2026, 10, 1)),
        _job('b', submitted: DateTime(2026, 10, 2)),
      ];
      expect(_ids(ReviewQueueState(requests: jobs).toReview), ['b', 'a']);

      final resubmitted = [_job('a', submitted: DateTime(2026, 10, 5)), jobs[1]];
      expect(_ids(ReviewQueueState(requests: resubmitted).toReview), ['a', 'b']);
    });

    test('a job submitted before submissions were timed falls back to its scheduled time', () {
      final state = ReviewQueueState(
        requests: [
          _job('untimed-old', scheduled: DateTime(2026, 9, 20)),
          _job('timed', submitted: DateTime(2026, 10, 3)),
          _job('untimed-new', scheduled: DateTime(2026, 9, 28)),
        ],
      );

      expect(_ids(state.toReview), ['timed', 'untimed-new', 'untimed-old']);
    });

    test('sends-back are ordered the same way, and the tabs stay separate', () {
      final state = ReviewQueueState(
        requests: [
          _job('returned-old', status: JobStatus.certificateReturned, submitted: DateTime(2026, 10, 1)),
          _job('returned-new', status: JobStatus.certificateReturned, submitted: DateTime(2026, 10, 4)),
          _job('waiting', submitted: DateTime(2026, 10, 2)),
          _job('sent', status: JobStatus.sentToClient, submitted: DateTime(2026, 10, 1)),
        ],
      );

      expect(_ids(state.returned), ['returned-new', 'returned-old']);
      expect(_ids(state.toReview), ['waiting']);
      expect(_ids(state.sent), ['sent']);
    });
  });
}
