import '../../../requests/domain/entities/inspection_request.dart';
import '../repositories/queue_repository.dart';

/// The signed-in user's queue: a coordinator's accepted quotes waiting on an
/// inspector, being inspected or awaiting their certificate; an inspector's own
/// jobs (the API scopes it by role).
class GetQueue {
  final QueueRepository _repository;

  const GetQueue(this._repository);

  Future<List<InspectionRequest>> call({String? managerId, String? status, String? query}) =>
      _repository.list(managerId: managerId, status: status, query: query);
}
