import '../entities/machine_form.dart';
import '../repositories/machines_repository.dart';

/// Saves what the inspector entered on a machine's form.
class SaveMachineForm {
  final MachinesRepository _repository;

  const SaveMachineForm(this._repository);

  Future<MachineForm> call(String requestId, String requestItemId, MachineFormChanges changes) =>
      _repository.saveMachineForm(requestId, requestItemId, changes);
}
