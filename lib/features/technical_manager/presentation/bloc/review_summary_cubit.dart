import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../auth/data/datasources/signature_local_datasource.dart';
import '../../../certificate/domain/entities/certificate_failure.dart';
import '../../../machines/domain/entities/certificate_incomplete_failure.dart';
import '../../../machines/domain/entities/job_machines.dart';
import '../../../machines/domain/entities/machine_status.dart';
import '../../../machines/domain/usecases/approve_all_machines.dart';
import '../../../machines/domain/usecases/get_job_machines.dart';
import '../../../machines/domain/usecases/return_job_to_inspector.dart';
import '../../../queue/domain/usecases/get_to_review.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';

part 'review_summary_state.dart';

/// Backs the technical manager's summary of one job: the job's `to-review`
/// row, every machine with a certificate to look at, and the two decisions on
/// the job — approve all (one call, all or nothing) or send the whole job
/// back to the inspector with a note.
///
/// Which buttons show comes from the machines API's job-level flags
/// (`canReview`, `canApprove`); while approving isn't possible its
/// `reviewBlockers` say why.
class ReviewSummaryCubit extends Cubit<ReviewSummaryState> {
  final GetToReview _getToReview;
  final GetJobMachines _getMachines;
  final ApproveAllMachines _approveAll;
  final ReturnJobToInspector _returnJob;
  final SignatureLocalDataSource _signatures;
  final String _userId;

  ReviewSummaryCubit({
    required InspectionRequest request,
    required String userId,
    required GetToReview getToReview,
    required GetJobMachines getJobMachines,
    required ApproveAllMachines approveAllMachines,
    required ReturnJobToInspector returnJobToInspector,
    required SignatureLocalDataSource signatures,
  }) : _getToReview = getToReview,
       _getMachines = getJobMachines,
       _approveAll = approveAllMachines,
       _returnJob = returnJobToInspector,
       _signatures = signatures,
       _userId = userId,
       super(ReviewSummaryState(request: request));

  Future<void> start() async {
    try {
      await _load();
    } catch (error) {
      if (isClosed) return;
      emit(state.copyWith(status: ReviewSummaryStatus.error, loadFailure: _requestsFailureOf(error)));
    }
  }

  Future<void> retry() {
    emit(state.copyWith(status: ReviewSummaryStatus.loading));
    return start();
  }

  /// Reads the job's row from the `to-review` list (keeping the one already
  /// held if the job has left it) and its machines.
  Future<void> _load() async {
    final rows = await _getToReview();
    final job = await _getMachines(state.request.id);
    if (isClosed) return;
    final row = rows.where((r) => r.id == state.request.id).firstOrNull;
    emit(
      state.copyWith(
        status: ReviewSummaryStatus.ready,
        request: row ?? state.request,
        machines: _withCertificate(job.machines),
        canReview: job.canReview,
        canApprove: job.canApprove,
        reviewBlockers: job.reviewBlockers,
        reviewHistory: job.reviewHistory,
      ),
    );
  }

  /// The machines that have a certificate to look at: submitted or already
  /// decided, the ones waiting on the manager first.
  static List<MachineSummary> _withCertificate(List<MachineSummary> machines) {
    final list = machines
        .where((m) => m.status != MachineStatus.notStarted && m.status != MachineStatus.draft)
        .toList();
    list.sort((a, b) => (b.canReview ? 1 : 0).compareTo(a.canReview ? 1 : 0));
    return list;
  }

  /// Approves every certificate of the job. The drawn signature kept in the
  /// profile goes along (and is remembered by the API); without one the API
  /// signs with the one it holds.
  Future<void> approveAll() async {
    if (!state.canApprove || state.isBusy) return;
    emit(state.copyWith(isApproving: true));
    try {
      List<List<double>>? strokes;
      try {
        strokes = await _signatures.readStrokes(_userId);
      } catch (_) {}
      await _approveAll(state.request.id, signature: strokes, rememberSignature: strokes != null);
      _finish(const ReviewAction(approved: true));
    } catch (error) {
      await _fail(approved: true, error: error);
    }
  }

  /// Sends the whole job back to the inspector with [note].
  Future<void> rejectAll(String note) async {
    if (!state.canReview || state.isBusy) return;
    emit(state.copyWith(isReturning: true));
    try {
      await _returnJob(state.request.id, note: note.trim());
      _finish(const ReviewAction(approved: false));
    } catch (error) {
      await _fail(approved: false, error: error);
    }
  }

  void _finish(ReviewAction action) {
    if (isClosed) return;
    emit(
      state.copyWith(
        isApproving: false,
        isReturning: false,
        actionSeq: state.actionSeq + 1,
        lastAction: action,
      ),
    );
  }

  /// Reports the failure, then reads the job again: what the API refused on
  /// (a machine without a submitted certificate, say) may show in its blockers
  /// now.
  Future<void> _fail({required bool approved, required Object error}) async {
    final action = ReviewAction(
      approved: approved,
      failure: _failureOf(error),
      problems: error is CertificateIncompleteFailure ? error.problems : const [],
    );
    try {
      await _load();
    } catch (_) {}
    _finish(action);
  }

  static RequestsFailureCode _requestsFailureOf(Object error) =>
      error is RequestsFailure ? error.code : RequestsFailureCode.unknown;

  static CertificateFailureCode _failureOf(Object error) {
    if (error is CertificateIncompleteFailure) return CertificateFailureCode.incomplete;
    if (error is CertificateFailure) return error.code;
    if (error is RequestsFailure) {
      return switch (error.code) {
        RequestsFailureCode.permissionDenied => CertificateFailureCode.permissionDenied,
        RequestsFailureCode.network => CertificateFailureCode.network,
        RequestsFailureCode.conflict || RequestsFailureCode.unknown => CertificateFailureCode.unknown,
      };
    }
    return CertificateFailureCode.unknown;
  }
}
