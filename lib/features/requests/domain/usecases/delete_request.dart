import '../repositories/requests_repository.dart';

/// Discards a "maybe" email the coordinator doesn't want as a request.
class DeleteRequest {
  final RequestsRepository _repository;

  const DeleteRequest(this._repository);

  Future<void> call(String requestId) => _repository.deleteRequest(requestId);
}
