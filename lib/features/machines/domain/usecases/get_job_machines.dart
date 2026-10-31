import '../entities/job_machines.dart';
import '../repositories/machines_repository.dart';

/// The machines to inspect on a job.
class GetJobMachines {
  final MachinesRepository _repository;

  const GetJobMachines(this._repository);

  Future<JobMachines> call(String requestId) => _repository.getMachines(requestId);
}
