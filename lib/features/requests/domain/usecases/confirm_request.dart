import '../entities/inspection_request.dart';
import '../repositories/requests_repository.dart';

/// Keeps a "maybe" email as a real request.
class ConfirmRequest {
  final RequestsRepository _repository;

  const ConfirmRequest(this._repository);

  Future<InspectionRequest> call(String requestId) =>
      _repository.confirmRequest(requestId);
}
