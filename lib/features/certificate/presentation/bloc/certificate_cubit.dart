import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../machines/domain/entities/certificate_incomplete_failure.dart';
import '../../../machines/domain/entities/machine_form.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../domain/certificate_progress.dart';
import '../../domain/certificate_template.dart';
import '../../domain/entities/certificate.dart';
import '../../domain/entities/certificate_failure.dart';
import '../../domain/entities/certificate_result.dart';
import '../../domain/entities/checklist_answer.dart';
import '../../domain/machine_certificate_mapper.dart';
import '../../domain/machine_certificate_source.dart';

part 'certificate_state.dart';

/// Backs Feature 05's inspection certificate screen: one machine of a job,
/// read, autosaved and submitted through the machines API — the report of
/// thorough examination, its checklist, and the API's say on when it can be
/// submitted.
class CertificateCubit extends Cubit<CertificateState> {
  final InspectionRequest request;
  final MachineCertificateSource _machine;

  // What the API holds, for working out what changed.
  MachineForm? _form;
  Certificate? _saved;
  Map<String, ({String value, String? note})> _savedChecklist = {};
  bool _started = false;
  Future<void>? _saving;

  static const _saveDelay = Duration(milliseconds: 400);

  CertificateCubit({required this.request, required MachineCertificateSource machine})
    : _machine = machine,
      super(const CertificateState());

  void start() {
    if (_started) return;
    _started = true;
    _load();
  }

  Future<void> retry() {
    emit(state.copyWith(status: CertificateStatus.loading, clearFailure: true));
    return _load();
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

  /// A checklist row's answer (`yes` / `no` / `na`). A
  /// "no" needs a note, so it is only sent once there is one; the others are
  /// saved right away.
  void setChecklistAnswer(String rowKey, String value) {
    final current = state.certificate;
    if (current == null) return;
    _emitChecklist(rowKey, value: value, keepNote: value == 'no');
    _persist(current);
  }

  /// The note on a "no" row. Local until [persist].
  void setChecklistNote(String rowKey, String note) {
    _emitChecklist(rowKey, value: null, note: note);
  }

  /// Writes whatever's currently in [CertificateState.certificate] — call
  /// after a batch of [setText] calls (i.e. on blur).
  Future<void> persist() async {
    final current = state.certificate;
    if (current == null || isClosed) return;
    await _persist(current);
  }

  Future<void> _persist(Certificate updated) => _persistMachine(updated);

  Future<void> submit() => _submitMachine();

  /// Leaving the screen right after an edit must not drop it: wait for the
  /// pending save before closing.
  @override
  Future<void> close() async {
    await _saving;
    return super.close();
  }

  // ---- The API ----------------------------------------------------------------

  Future<void> _load() async {
    final machine = _machine;
    try {
      final form = await machine.getForm(request.id, machine.requestItemId);
      if (isClosed) return;
      _form = form;
      final certificate = MachineCertificateMapper.toCertificate(form);
      _saved = certificate;
      _savedChecklist = MachineCertificateMapper.checklistOf(form.checklist);
      emit(
        state.copyWith(
          status: CertificateStatus.ready,
          certificate: certificate,
          clearFailure: true,
          machineMode: true,
          machineFields: form.fields,
          checklist: form.checklist,
          inspectedItem: form.header.inspectedItem,
          apiReady: form.canEdit && form.readyToSubmit,
          missing: form.missing,
        ),
      );
    } catch (error) {
      if (isClosed) return;
      emit(state.copyWith(status: CertificateStatus.error, failure: _failureOf(error)));
    }
  }

  /// Sends what changed since the last save: the form's texts, dates and
  /// answers, and the checklist rows. The API answers with whether the
  /// machine can be submitted yet.
  ///
  /// One save runs at a time: edits made while a PUT is in flight are picked
  /// up by the next pass of [_saveChanges], which diffs against what the API
  /// has acknowledged — so the same change is never sent twice. A save also
  /// waits a moment before it starts, so a burst of edits (a field's blur and
  /// the option tapped right after it) goes out as one PUT. The returned
  /// future completes once everything made so far is saved.
  Future<void> _persistMachine(Certificate updated) {
    if (_form == null || _saved == null) return Future.value();
    emit(state.copyWith(certificate: updated, saveStatus: CertificateSaveStatus.saving));
    return _saving ??= _saveChanges().whenComplete(() => _saving = null);
  }

  Future<void> _saveChanges() async {
    final machine = _machine;
    await Future<void>.delayed(_saveDelay);
    while (!isClosed) {
      final form = _form;
      final saved = _saved;
      final current = state.certificate;
      if (form == null || saved == null || current == null) return;

      final changes = MachineCertificateMapper.diff(
        saved: saved,
        current: current,
        fieldKeys: {for (final field in form.fields) field.key},
        checklist: state.checklist,
        savedChecklist: _savedChecklist,
      );
      if (changes.isEmpty) {
        emit(state.copyWith(saveStatus: CertificateSaveStatus.saved));
        return;
      }

      try {
        final result = await machine.saveForm(request.id, machine.requestItemId, changes);
        if (isClosed) return;
        _saved = current;
        _savedChecklist = {..._savedChecklist, ...changes.checklist};
        emit(
          state.copyWith(
            saveStatus: CertificateSaveStatus.saved,
            apiReady: result.canEdit && result.readyToSubmit,
            missing: result.missing,
          ),
        );
      } catch (_) {
        if (isClosed) return;
        emit(state.copyWith(saveStatus: CertificateSaveStatus.error));
        return;
      }
    }
  }

  Future<void> _submitMachine() async {
    final machine = _machine;
    final certificate = state.certificate;
    if (certificate == null || !state.canSubmit) return;

    emit(state.copyWith(isSubmitting: true));
    await _persistMachine(certificate);
    try {
      await machine.submit(request.id, machine.requestItemId);
      if (isClosed) return;
      emit(state.copyWith(isSubmitting: false, actionSeq: state.actionSeq + 1, lastActionSuccess: true));
    } catch (error) {
      if (isClosed) return;
      emit(
        state.copyWith(
          isSubmitting: false,
          actionSeq: state.actionSeq + 1,
          lastActionSuccess: false,
          lastActionFailure: _failureOf(error),
          // The API's own list of what is missing replaces the earlier one.
          apiReady: error is CertificateIncompleteFailure ? false : null,
          missing: error is CertificateIncompleteFailure ? error.problems : null,
        ),
      );
    }
  }

  /// Sets a checklist row's [value] (when given) or [note] locally.
  void _emitChecklist(String rowKey, {String? value, bool keepNote = false, String? note}) {
    emit(
      state.copyWith(
        checklist: [
          for (final section in state.checklist)
            MachineFormSection(
              key: section.key,
              title: section.title,
              items: [
                for (final row in section.items)
                  row.key == rowKey
                      ? MachineFormRow(
                          key: row.key,
                          label: row.label,
                          value: value ?? row.value,
                          note: note ?? (value == null || keepNote ? row.note : null),
                        )
                      : row,
              ],
            ),
        ],
      ),
    );
  }

  static CertificateFailureCode _failureOf(Object error) {
    if (error is CertificateFailure) return error.code;
    if (error is CertificateIncompleteFailure) return CertificateFailureCode.incomplete;
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
