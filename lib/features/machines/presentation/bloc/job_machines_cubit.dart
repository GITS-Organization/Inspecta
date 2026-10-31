import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../requests/domain/entities/requests_failure.dart';
import '../../domain/entities/job_machines.dart';
import '../../domain/usecases/get_job_machines.dart';
import '../../domain/usecases/start_job_inspection.dart';

part 'job_machines_state.dart';

/// Backs the screen listing a job's machines: their certificate progress and
/// the "Start inspection" button.
class JobMachinesCubit extends Cubit<JobMachinesState> {
  final String requestId;
  final GetJobMachines _getMachines;
  final StartJobInspection _startInspection;

  /// Bumped per fetch so a slow, superseded response can't overwrite a newer
  /// one.
  int _fetch = 0;

  JobMachinesCubit({
    required this.requestId,
    required GetJobMachines getMachines,
    required StartJobInspection startInspection,
  }) : _getMachines = getMachines,
       _startInspection = startInspection,
       super(const JobMachinesState());

  /// Loads the machines — also the pull-to-refresh and the reload on return
  /// from a machine. A failed reload keeps what is on screen; only a first
  /// load shows the error.
  Future<void> start() async {
    final fetch = ++_fetch;
    try {
      final machines = await _getMachines(requestId);
      if (isClosed || fetch != _fetch) return;
      emit(state.copyWith(status: JobMachinesStatus.ready, machines: machines, clearFailure: true));
    } catch (error) {
      if (isClosed || fetch != _fetch) return;
      if (state.status == JobMachinesStatus.ready) return;
      emit(state.copyWith(status: JobMachinesStatus.error, failure: _codeOf(error)));
    }
  }

  Future<void> retry() {
    emit(state.copyWith(status: JobMachinesStatus.loading, clearFailure: true));
    return start();
  }

  /// Begins the inspection, then reloads the machines. Returns the failure's
  /// code when it didn't work, `null` when it did.
  Future<RequestsFailureCode?> startInspection() async {
    if (state.isStarting) return null;
    emit(state.copyWith(isStarting: true));
    try {
      await _startInspection(requestId);
      if (isClosed) return null;
      emit(state.copyWith(isStarting: false));
      await start();
      return null;
    } catch (error) {
      if (!isClosed) emit(state.copyWith(isStarting: false));
      return _codeOf(error);
    }
  }

  static RequestsFailureCode _codeOf(Object error) =>
      error is RequestsFailure ? error.code : RequestsFailureCode.unknown;
}
