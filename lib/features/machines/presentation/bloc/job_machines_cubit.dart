import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../requests/domain/entities/requests_failure.dart';
import '../../domain/entities/certificate_incomplete_failure.dart';
import '../../domain/entities/job_machines.dart';
import '../../domain/usecases/get_job_machines.dart';
import '../../domain/usecases/start_job_inspection.dart';
import '../../domain/usecases/submit_all_machines.dart';

part 'job_machines_state.dart';

/// Backs the screen listing a job's machines: their certificate progress and
/// the "Start inspection" button.
class JobMachinesCubit extends Cubit<JobMachinesState> {
  final String requestId;
  final GetJobMachines _getMachines;
  final StartJobInspection _startInspection;
  final SubmitAllMachines _submitAll;

  /// Bumped per fetch so a slow, superseded response can't overwrite a newer
  /// one.
  int _fetch = 0;

  JobMachinesCubit({
    required this.requestId,
    required GetJobMachines getMachines,
    required StartJobInspection startInspection,
    required SubmitAllMachines submitAll,
  }) : _getMachines = getMachines,
       _startInspection = startInspection,
       _submitAll = submitAll,
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
  ///
  /// [JobMachinesState.isStarting] stays up through the reload too: until the
  /// machines come back the screen still offers "Start inspection", so the
  /// button must keep spinning (and ignore taps) rather than flick back to
  /// idle and let the inspection be started twice.
  Future<RequestsFailureCode?> startInspection() async {
    if (state.isStarting) return null;
    emit(state.copyWith(isStarting: true));
    try {
      await _startInspection(requestId);
      // Never throws: a failed reload keeps what is on screen.
      await start();
      return null;
    } catch (error) {
      return _codeOf(error);
    } finally {
      if (!isClosed) emit(state.copyWith(isStarting: false));
    }
  }

  /// Sends every certificate of the job to the technical manager together,
  /// then reloads the machines. Nothing is sent unless every machine is ready:
  /// the outcome then lists what is missing on each one.
  Future<SubmitAllOutcome> submitAll() async {
    if (state.isSubmitting) return const SubmitAllOutcome();
    emit(state.copyWith(isSubmitting: true));
    try {
      await _submitAll(requestId);
      await start();
      return const SubmitAllOutcome(sent: true);
    } on CertificateIncompleteFailure catch (incomplete) {
      return SubmitAllOutcome(problems: incomplete.problems);
    } catch (error) {
      return SubmitAllOutcome(failure: _codeOf(error));
    } finally {
      if (!isClosed) emit(state.copyWith(isSubmitting: false));
    }
  }

  static RequestsFailureCode _codeOf(Object error) =>
      error is RequestsFailure ? error.code : RequestsFailureCode.unknown;
}

/// How "Submit all" ended: [sent], or [problems] (what each unready machine
/// still needs), or a [failure] of another kind. All empty when a second tap
/// arrived while one was already running.
class SubmitAllOutcome {
  final bool sent;
  final List<String> problems;
  final RequestsFailureCode? failure;

  const SubmitAllOutcome({this.sent = false, this.problems = const [], this.failure});
}
