import '../../../requests/domain/entities/inspection_request.dart';
import '../repositories/queue_repository.dart';

/// The coordinator's queue: accepted quotes waiting on an inspector, being
/// inspected, or past inspection and awaiting their certificate.
class GetCoordinatorQueue {
  final QueueRepository _repository;

  const GetCoordinatorQueue(this._repository);

  Future<List<InspectionRequest>> call({String? managerId}) =>
      _repository.list(managerId: managerId);
}
