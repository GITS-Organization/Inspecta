import '../entities/machine_form.dart';
import '../repositories/machines_repository.dart';

/// One machine's whole form.
class GetMachineForm {
  final MachinesRepository _repository;

  const GetMachineForm(this._repository);

  Future<MachineForm> call(String requestId, String requestItemId) =>
      _repository.getMachineForm(requestId, requestItemId);
}
