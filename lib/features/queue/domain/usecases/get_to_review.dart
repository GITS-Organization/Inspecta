import '../../../requests/domain/entities/inspection_request.dart';
import '../repositories/queue_repository.dart';

/// The jobs whose machine certificates wait on the technical manager's
/// review, with each job's certificate counts.
class GetToReview {
  final QueueRepository _repository;

  const GetToReview(this._repository);

  Future<List<InspectionRequest>> call({String? managerId, String? inspectorId}) =>
      _repository.toReview(managerId: managerId, inspectorId: inspectorId);
}
