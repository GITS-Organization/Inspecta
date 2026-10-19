import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../auth/domain/entities/user.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../domain/certificate_defaults.dart';
import '../../domain/certificate_progress.dart';
import '../../domain/certificate_template.dart';
import '../../domain/entities/certificate.dart';
import '../../domain/entities/certificate_failure.dart';
import '../../domain/entities/certificate_result.dart';
import '../../domain/entities/checklist_answer.dart';
import '../../domain/usecases/save_certificate_draft.dart';
import '../../domain/usecases/submit_certificate.dart';
import '../../domain/usecases/watch_certificate.dart';

part 'certificate_state.dart';

/// Backs Feature 05's inspection certificate screen: one job's report of
/// thorough examination — autosaved on every discrete change, submitted
/// once every section is filled in.
class CertificateCubit extends Cubit<CertificateState> {
  final InspectionRequest request;
  final AppUser? _inspector;
  final WatchCertificate _watchCertificate;
  final SaveCertificateDraft _saveDraft;
  final SubmitCertificate _submitCertificate;

  StreamSubscription<Certificate?>? _subscription;

  CertificateCubit({
    required this.request,
    required AppUser? inspector,
    required WatchCertificate watchCertificate,
    required SaveCertificateDraft saveDraft,
    required SubmitCertificate submitCertificate,
  }) : _inspector = inspector,
       _watchCertificate = watchCertificate,
       _saveDraft = saveDraft,
       _submitCertificate = submitCertificate,
       super(const CertificateState());

  void start() {
    if (_subscription != null) return;
    _subscription = _watchCertificate(request.id).listen(
      (certificate) {
        // First time this job is opened there's no document yet — and one
        // started on an older template has nothing the new form can use.
        // Either way, seed it with what the job already tells us and save
        // it, so the rest of the app (and this inspector, reopening later)
        // has something to read.
        final needsSeed = certificate == null || certificate.templateId != CertificateTemplate.id;
        final resolved = needsSeed
            ? seedCertificate(
                certificate ?? Certificate(requestId: request.id, inspectorId: request.inspectorId ?? ''),
                request: request,
                inspector: _inspector,
              )
            : certificate;
        emit(state.copyWith(status: CertificateStatus.ready, certificate: resolved, clearFailure: true));
        if (needsSeed) _saveDraft(resolved);
      },
      onError: (Object error) => emit(
        state.copyWith(
          status: CertificateStatus.error,
          failure: error is CertificateFailure ? error.code : CertificateFailureCode.unknown,
        ),
      ),
    );
  }

  Future<void> retry() async {
    await _subscription?.cancel();
    _subscription = null;
    emit(state.copyWith(status: CertificateStatus.loading, clearFailure: true));
    start();
  }

  /// Text fields update local state on every keystroke (so completeness
  /// and the step indicator react live) but only autosave on blur, via
  /// [persist] — otherwise every keystroke would be its own write.
  void setText(String key, String value) {
    final current = state.certificate;
    if (current == null) return;
    emit(state.copyWith(certificate: current.withText(key, value)));
  }

  /// A date is a discrete choice — persist right away. Moving the
  /// examination date also moves the next-examination date along, as long
  /// as the inspector hasn't picked a different one themselves.
  void setDate(String key, DateTime? value) {
    final current = state.certificate;
    if (current == null) return;
    var updated = current.withDate(key, value);
    if (key == CertDate.examination && value != null) {
      final previous = current.dateOf(CertDate.examination);
      final next = current.dateOf(CertDate.nextExamination);
      final followsSuggestion =
          next == null || (previous != null && next == CertificateTemplate.suggestedNextExamination(previous));
      if (followsSuggestion) {
        updated = updated.withDate(CertDate.nextExamination, CertificateTemplate.suggestedNextExamination(value));
      }
    }
    _persist(updated);
  }

  /// The 6-month and 12-month interval questions are opposites (see
  /// [CertQuestion.intervalCounterpart]), so answering one answers the other.
  void setAnswer(String question, bool answer) {
    final current = state.certificate;
    if (current == null) return;
    var updated = current.withAnswer(question, answer);
    final counterpart = CertQuestion.intervalCounterpart(question);
    if (counterpart != null) updated = updated.withAnswer(counterpart, !answer);
    _persist(updated);
  }

  void setFunctionCheck(ChecklistAnswer answer) {
    final current = state.certificate;
    if (current == null) return;
    _persist(current.copyWith(functionCheck: answer));
  }

  void setFinalResult(CertificateResult result) {
    final current = state.certificate;
    if (current == null) return;
    _persist(current.copyWith(finalResult: result));
  }

  /// Writes whatever's currently in [CertificateState.certificate] — call
  /// after a batch of [setText] calls (i.e. on blur).
  Future<void> persist() async {
    final current = state.certificate;
    if (current == null) return;
    await _persist(current);
  }

  Future<void> _persist(Certificate updated) async {
    emit(state.copyWith(certificate: updated, saveStatus: CertificateSaveStatus.saving));
    try {
      await _saveDraft(updated);
      if (isClosed) return;
      emit(state.copyWith(saveStatus: CertificateSaveStatus.saved));
    } catch (e) {
      if (isClosed) return;
      emit(state.copyWith(saveStatus: CertificateSaveStatus.error));
    }
  }

  Future<void> submit() async {
    final certificate = state.certificate;
    if (certificate == null || !certificate.isReadyToSubmit || state.isSubmitting) return;
    emit(state.copyWith(isSubmitting: true));
    try {
      await _submitCertificate(withPreparedBy(certificate, _inspector));
      if (isClosed) return;
      emit(state.copyWith(isSubmitting: false, actionSeq: state.actionSeq + 1, lastActionSuccess: true));
    } catch (e) {
      if (isClosed) return;
      emit(
        state.copyWith(
          isSubmitting: false,
          actionSeq: state.actionSeq + 1,
          lastActionSuccess: false,
          lastActionFailure: e is CertificateFailure ? e.code : CertificateFailureCode.unknown,
        ),
      );
    }
  }

  @override
  Future<void> close() async {
    await _subscription?.cancel();
    return super.close();
  }
}
