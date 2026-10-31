import '../repositories/machines_repository.dart';

/// The inspector arrives and begins: the job moves to in progress.
class StartJobInspection {
  final MachinesRepository _repository;

  const StartJobInspection(this._repository);

  Future<void> call(String requestId) => _repository.startInspection(requestId);
}
