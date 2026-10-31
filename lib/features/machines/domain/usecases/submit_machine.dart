import '../repositories/machines_repository.dart';

/// Submits a machine's certificate to the technical manager.
class SubmitMachine {
  final MachinesRepository _repository;

  const SubmitMachine(this._repository);

  Future<void> call(String requestId, String requestItemId) =>
      _repository.submitMachine(requestId, requestItemId);
}
