import '../repositories/machines_repository.dart';

/// The technical manager approves a machine's certificate.
class ApproveMachine {
  final MachinesRepository _repository;

  const ApproveMachine(this._repository);

  Future<void> call(String requestId, String requestItemId) =>
      _repository.approveMachine(requestId, requestItemId);
}
