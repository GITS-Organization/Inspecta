import '../repositories/machines_repository.dart';

/// The technical manager approves every certificate of a job in one call.
class ApproveAllMachines {
  final MachinesRepository _repository;

  const ApproveAllMachines(this._repository);

  Future<void> call(String requestId, {List<List<double>>? signature, bool rememberSignature = false}) =>
      _repository.approveAll(requestId, signature: signature, rememberSignature: rememberSignature);
}
