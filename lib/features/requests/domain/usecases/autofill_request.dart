import '../entities/inspection_request.dart';
import '../repositories/requests_repository.dart';

/// Reads a request's email again and adds the machines it clearly names, when
/// the request has a client and no items yet.
class AutofillRequest {
  final RequestsRepository _repository;

  const AutofillRequest(this._repository);

  Future<InspectionRequest> call(String requestId) => _repository.autofill(requestId);
}
