import '../repositories/machines_repository.dart';

/// The technical manager sends a whole job back to its inspector with a note.
class ReturnJobToInspector {
  final MachinesRepository _repository;

  const ReturnJobToInspector(this._repository);

  Future<void> call(String requestId, {required String note, Map<String, String> machineComments = const {}}) =>
      _repository.returnToInspector(requestId, note: note, machineComments: machineComments);
}
