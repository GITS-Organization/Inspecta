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
    conclusionKey,
  };

  /// The API keeps the verdict as the `conclusion` text.
  static const conclusionKey = 'conclusion';

  static const _verdictPhrases = {
    CertificateResult.safeToOperate: 'Safe to operate',
    CertificateResult.safeWithConditions: 'Safe to operate with conditions',
    CertificateResult.notSafe: 'Not safe to operate',
  };

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
      finalResult: verdictOf(texts[conclusionKey]),
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
    final phrase = _verdictPhrases[current.finalResult];
    if (current.finalResult != saved.finalResult && phrase != null) {
      texts[conclusionKey] = phrase;
    }

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

    return MachineFormChanges(fields: fields, checklist: rows, answers: answers, texts: texts, dates: dates);
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

  static CertificateResult? verdictOf(String? conclusion) {
    final text = conclusion?.trim().toLowerCase();
    if (text == null || text.isEmpty) return null;
    for (final entry in _verdictPhrases.entries) {
      if (entry.value.toLowerCase() == text) return entry.key;
    }
    return null;
  }

  static String formatDate(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
}
