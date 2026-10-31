import '../entities/delivery.dart';
import '../repositories/machines_repository.dart';

/// The email the coordinator is about to hand over with the job's
/// certificates.
class GetDeliveryPreview {
  final MachinesRepository _repository;

  const GetDeliveryPreview(this._repository);

  Future<DeliveryPreview> call(String requestId) => _repository.getDeliveryPreview(requestId);
}
