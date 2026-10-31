import '../repositories/machines_repository.dart';

/// The technical manager sends a machine's certificate back to its inspector.
class ReturnMachine {
  final MachinesRepository _repository;

  const ReturnMachine(this._repository);

  Future<void> call(String requestId, String requestItemId, {required String note}) =>
      _repository.returnMachine(requestId, requestItemId, note: note);
}
