import '../../machines/domain/entities/machine_form.dart';
import 'certificate_template.dart';
import 'entities/certificate.dart';
import 'entities/certificate_result.dart';
import 'entities/checklist_answer.dart';

/// Translates between the certificate form's [Certificate] and a machine's
/// form on the API: the form keeps working on a [Certificate], and only what
/// the API understands is sent back.
abstract final class MachineCertificateMapper {
  /// The free-text keys `PUT …/machines/{id}` accepts under `texts`; anything
  /// else a form field holds is either one of the machine's own `fields` or
  /// not saved.
  static const _apiTexts = {
    CertText.testType,
    CertText.defectDescription,
    CertText.repairsRequired,
    CertText.testsCarriedOut,
    CertText.note,
    CertText.clientRepresentative,
  };

  /// The API keeps the verdict as `result`: `satisfactory` or
  /// `unsatisfactory`, required before a machine can be submitted. (Its
  /// `conclusion` text is not printed on the certificate, so it isn't used.)
  static const _satisfactory = 'satisfactory';
  static const _unsatisfactory = 'unsatisfactory';

  /// The form as a [Certificate]: the API's texts and the machine's own
  /// fields share the texts map, keyed as the API keys them.
  static Certificate toCertificate(MachineForm form) {
    final texts = <String, String>{...form.texts};
    for (final field in form.fields) {
      texts[field.key] = field.value;
    }
    final header = form.header;
    if ((texts[CertText.clientRepresentative] ?? '').trim().isEmpty && header.clientRepresentative.isNotEmpty) {
      texts[CertText.clientRepresentative] = header.clientRepresentative;
    }
    if (header.standards.isNotEmpty) {
      texts[CertText.standardOfInspection] = header.standards.join(', ');
    }

    final dates = <String, DateTime>{};
    for (final entry in form.dates.entries) {
      final date = DateTime.tryParse(entry.value);
      if (date != null) dates[entry.key] = DateTime(date.year, date.month, date.day);
    }

    return Certificate(
      requestId: form.requestId,
      inspectorId: '',
      texts: texts,
      dates: dates,
      answers: form.answers,
      functionCheck: functionCheckOf(form.checklist),
      finalResult: verdictOf(form.result),
      reviewNote: form.reviewNote,
      certificateNumber: form.certificateNumber,
    );
  }

  /// What changed from [saved] to [current], and from [savedChecklist] to the
  /// rows now in [checklist]. A "no" row without a note waits for one, since
  /// the API refuses it.
  static MachineFormChanges diff({
    required Certificate saved,
    required Certificate current,
    required Set<String> fieldKeys,
    required List<MachineFormSection> checklist,
    required Map<String, ({String value, String? note})> savedChecklist,
  }) {
    final fields = <String, String>{};
    final texts = <String, String>{};
    for (final entry in current.texts.entries) {
      if (entry.value == (saved.texts[entry.key] ?? '')) continue;
      if (fieldKeys.contains(entry.key)) {
        fields[entry.key] = entry.value;
      } else if (_apiTexts.contains(entry.key)) {
        texts[entry.key] = entry.value;
      }
    }
    final result = current.finalResult == saved.finalResult ? null : resultOf(current.finalResult);

    final dates = <String, String>{
      for (final entry in current.dates.entries)
        if (saved.dates[entry.key] != entry.value) entry.key: formatDate(entry.value),
    };

    final answers = <String, bool>{
      for (final entry in current.answers.entries)
        if (saved.answers[entry.key] != entry.value) entry.key: entry.value,
    };

    final rows = <String, ({String value, String? note})>{};
    for (final section in checklist) {
      for (final row in section.items) {
        final value = normalizeAnswer(row.value);
        if (value.isEmpty) continue;
        final note = value == 'no' ? row.note?.trim() : null;
        if (value == 'no' && (note == null || note.isEmpty)) continue;
        final next = (value: value, note: note);
        if (savedChecklist[row.key] != next) rows[row.key] = next;
      }
    }

    return MachineFormChanges(
      fields: fields,
      checklist: rows,
      answers: answers,
      texts: texts,
      dates: dates,
      result: result,
    );
  }

  /// The checklist rows as saved: what [diff] compares against.
  static Map<String, ({String value, String? note})> checklistOf(List<MachineFormSection> checklist) => {
    for (final section in checklist)
      for (final row in section.items)
        if (normalizeAnswer(row.value).isNotEmpty)
          row.key: (
            value: normalizeAnswer(row.value),
            note: normalizeAnswer(row.value) == 'no' ? row.note?.trim() : null,
          ),
  };

  /// `yes` / `no` / `na`, or empty for an unanswered row.
  static String normalizeAnswer(String value) => switch (value.toLowerCase()) {
    'yes' || 'pass' => 'yes',
    'no' || 'fail' => 'no',
    'na' || 'n/a' || 'not_applicable' => 'na',
    _ => '',
  };

  /// Whether any row still lacks a yes / no answer: unanswered, or N/A, which
  /// the checklist no longer offers.
  static bool needsDefaultAnswers(List<MachineFormSection> checklist) =>
      checklist.any((section) => section.items.any((row) => !_isYesOrNo(row)));

  /// [checklist] with every row that lacks a yes / no answer set to yes, the
  /// answer a row starts with. Answered rows are kept as they are.
  static List<MachineFormSection> withDefaultAnswers(List<MachineFormSection> checklist) => [
    for (final section in checklist)
      MachineFormSection(
        key: section.key,
        title: section.title,
        items: [
          for (final row in section.items)
            _isYesOrNo(row) ? row : MachineFormRow(key: row.key, label: row.label, value: 'yes'),
        ],
      ),
  ];

  static bool _isYesOrNo(MachineFormRow row) {
    final answer = normalizeAnswer(row.value);
    return answer == 'yes' || answer == 'no';
  }

  /// The old "function check", now what the checklist adds up to: unanswered
  /// until every row is answered, then fail if any is a no.
  static ChecklistAnswer functionCheckOf(List<MachineFormSection> checklist) {
    final answers = [
      for (final section in checklist)
        for (final row in section.items) normalizeAnswer(row.value),
    ];
    if (answers.any((answer) => answer.isEmpty)) return ChecklistAnswer.unanswered;
    return answers.contains('no') ? ChecklistAnswer.fail : ChecklistAnswer.pass;
  }

  /// The form's verdict for the API's `result`; null while none is chosen.
  static CertificateResult? verdictOf(String? result) => switch (result?.trim().toLowerCase()) {
    _satisfactory => CertificateResult.safeToOperate,
    _unsatisfactory => CertificateResult.notSafe,
    _ => null,
  };

  /// The API's `result` for the form's verdict.
  static String? resultOf(CertificateResult? verdict) => switch (verdict) {
    CertificateResult.safeToOperate || CertificateResult.safeWithConditions => _satisfactory,
    CertificateResult.notSafe => _unsatisfactory,
    null => null,
  };

  static String formatDate(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
}
