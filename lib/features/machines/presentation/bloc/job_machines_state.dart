part of 'job_machines_cubit.dart';

enum JobMachinesStatus { loading, ready, error }

class JobMachinesState extends Equatable {
  final JobMachinesStatus status;
  final JobMachines? machines;
  final RequestsFailureCode? failure;

  /// "Start inspection" is in flight.
  final bool isStarting;

  const JobMachinesState({
    this.status = JobMachinesStatus.loading,
    this.machines,
    this.failure,
    this.isStarting = false,
  });

  JobMachinesState copyWith({
    JobMachinesStatus? status,
    JobMachines? machines,
    RequestsFailureCode? failure,
    bool clearFailure = false,
    bool? isStarting,
  }) {
    return JobMachinesState(
      status: status ?? this.status,
      machines: machines ?? this.machines,
      failure: clearFailure ? null : (failure ?? this.failure),
      isStarting: isStarting ?? this.isStarting,
    );
  }

  @override
  List<Object?> get props => [status, machines, failure, isStarting];
}
