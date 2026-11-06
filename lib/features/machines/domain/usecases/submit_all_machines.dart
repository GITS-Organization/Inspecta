import '../repositories/machines_repository.dart';

/// Sends every certificate of a job to the technical manager together.
class SubmitAllMachines {
  final MachinesRepository _repository;

  const SubmitAllMachines(this._repository);

  Future<void> call(String requestId) => _repository.submitAllMachines(requestId);
}
