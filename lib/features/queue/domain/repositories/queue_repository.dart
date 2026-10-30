import '../../../requests/domain/entities/inspection_request.dart';

/// Methods throw [RequestsFailure] on expected errors.
abstract interface class QueueRepository {
  /// One page of the coordinator's queue from the API, every stage at once:
  /// jobs whose quote was accepted, from waiting on an inspector to
  /// certified, optionally only one [managerId]'s (Admin only).
  Future<List<InspectionRequest>> list({String? managerId, int page = 1, int pageSize = 50});
}
