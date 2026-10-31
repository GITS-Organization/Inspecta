import '../../../requests/domain/entities/inspection_request.dart';

/// Methods throw [RequestsFailure] on expected errors.
abstract interface class QueueRepository {
  /// One page of the coordinator's queue from the API, every stage at once:
  /// jobs whose quote was accepted, from waiting on an inspector to
  /// certified, optionally only one [managerId]'s (Admin only), only the
  /// list named [status] (`unassigned`, `certify`…) and only what matches
  /// [query].
  Future<List<InspectionRequest>> list({
    String? managerId,
    String? status,
    String? query,
    int page = 1,
    int pageSize = 50,
  });

  /// One page of the jobs with certificates waiting on the technical
  /// manager's review (`GET /quotation-queue/to-review`), optionally only
  /// one [managerId]'s or [inspectorId]'s.
  Future<List<InspectionRequest>> toReview({
    String? managerId,
    String? inspectorId,
    int page = 1,
    int pageSize = 50,
  });
}
