import '../entities/delivery.dart';
import '../repositories/machines_repository.dart';

/// The coordinator hands a job's approved certificates to the client.
class DeliverToClient {
  final MachinesRepository _repository;

  const DeliverToClient(this._repository);

  Future<DeliveryResult> call(
    String requestId, {
    String? sentTo,
    List<String>? cc,
    String? subject,
    String? message,
    List<String>? excludedReportIds,
  }) => _repository.deliverToClient(
    requestId,
    sentTo: sentTo,
    cc: cc,
    subject: subject,
    message: message,
    excludedReportIds: excludedReportIds,
  );
}
