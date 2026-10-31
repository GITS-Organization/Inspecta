import '../../domain/entities/delivery.dart';
import '../../domain/entities/job_machines.dart';
import '../../domain/entities/machine_form.dart';
import '../../domain/entities/machine_status.dart';

/// Reads the machines API's JSON into entities. Every field is optional on
/// the wire, so each reads defensively.
class MachinesParser {
  MachinesParser._();

  static String _string(Object? value) => value is String ? value : '';

  static String? _nullableString(Object? value) => value is String && value.isNotEmpty ? value : null;

  static int _int(Object? value) => value is num ? value.toInt() : 0;

  static bool _bool(Object? value) => value is bool && value;

  static List<Map<String, dynamic>> _maps(Object? value) =>
      value is List ? value.whereType<Map<String, dynamic>>().toList() : const [];

  static Map<String, String> _stringMap(Object? value) => value is Map
      ? {
          for (final entry in value.entries)
            if (entry.value is String) entry.key.toString(): entry.value as String,
        }
      : const {};

  static DeliveryPreview deliveryPreview(Map<String, dynamic> json) {
    // An attachment is a file name, or an object naming the file.
    String? attachment(Object? value) {
      final text = value is Map ? value['fileName'] ?? value['name'] ?? value['filename'] : value;
      final trimmed = text?.toString().trim();
      return trimmed == null || trimmed.isEmpty ? null : trimmed;
    }

    final to = json['to'];
    return DeliveryPreview(
      // `to` is an address, or a list of them.
      to: to is List ? to.whereType<String>().join(', ') : _nullableString(to),
      subject: _string(json['subject']),
      text: _string(json['text'] ?? json['body']),
      attachments: [
        if (json['attachments'] is List)
          for (final item in json['attachments'] as List)
            if (attachment(item) case final name?) name,
      ],
    );
  }

  static DeliveryResult deliveryResult(Object? json) {
    final delivery = json is Map ? json['delivery'] : null;
    return DeliveryResult(emailSent: delivery is Map && delivery['emailSent'] == true);
  }

  static List<String> _strings(Object? value) =>
      value is List ? [for (final v in value) if (v is String && v.isNotEmpty) v] : const [];

  /// One blocker as the API sends it: a sentence, or an object carrying one.
  static String? _blocker(Object? value) {
    final text = value is Map ? value['message'] ?? value['reason'] ?? value['label'] : value;
    final trimmed = text?.toString().trim();
    return trimmed == null || trimmed.isEmpty ? null : trimmed;
  }

  /// One earlier comment: a sentence, or an object with the text, who wrote
  /// it and when. `null` when there's no text.
  static ReviewComment? _comment(Object? value) {
    if (value is String) return value.trim().isEmpty ? null : ReviewComment(text: value.trim());
    if (value is! Map) return null;
    String? pick(List<String> keys) {
      for (final key in keys) {
        final v = value[key];
        if (v is String && v.trim().isNotEmpty) return v.trim();
      }
      return null;
    }

    final text = pick(['note', 'comment', 'message', 'text']);
    if (text == null) return null;
    final at = pick(['createdAt', 'at', 'date', 'returnedAt']);
    return ReviewComment(
      text: text,
      author: pick(['reviewerName', 'authorName', 'by', 'userName', 'author']),
      at: at == null ? null : DateTime.tryParse(at),
    );
  }

  static JobMachines jobMachines(Map<String, dynamic> json) {
    final progress = json['progress'];
    final scheduledAt = json['scheduledAt'];
    final machines = [
      for (final machine in _maps(json['machines']))
        MachineSummary(
          requestItemId: _string(machine['requestItemId']),
          name: _string(machine['name']),
          quantity: _int(machine['quantity']),
          serialNumber: _nullableString(machine['serialNumber']),
          templateKey: _nullableString(machine['templateKey']),
          status: MachineStatus.fromValue(machine['status']),
          checklistTotal: _int(machine['checklistTotal']),
          checklistAnswered: _int(machine['checklistAnswered']),
          reviewNote: _nullableString(machine['reviewNote']),
          certificateNumber: _nullableString(machine['certificateNumber']),
          canEdit: _bool(machine['canEdit']),
          canReview: _bool(machine['canReview']),
          type: _nullableString(machine['type']),
          templateName: _nullableString(machine['templateName']),
          manufacturer: _nullableString(machine['manufacturer']),
          capacity: _nullableString(machine['capacity']),
          model: _nullableString(machine['model']),
          inspectionStandards: _strings(machine['inspectionStandards']),
          examinationDate: _nullableString(machine['examinationDate']),
          nextExaminationDate: _nullableString(machine['nextExaminationDate']),
          result: _nullableString(machine['result']),
        ),
    ];
    // The job-level flags are newer than the per-machine ones: when the API
    // leaves them out, fall back to what the machines say.
    final canReview = json['canReview'] is bool ? json['canReview'] as bool : machines.any((m) => m.canReview);
    final canApprove = json['canApprove'] is bool ? json['canApprove'] as bool : canReview;

    return JobMachines(
      requestId: _string(json['requestId']),
      requestStatus: _string(json['requestStatus']),
      clientName: _string(json['clientName']),
      location: _string(json['location']),
      scheduledAt: scheduledAt is String ? DateTime.tryParse(scheduledAt) : null,
      progress: progress is Map<String, dynamic>
          ? MachineProgress(
              total: _int(progress['total']),
              submitted: _int(progress['submitted']),
              approved: _int(progress['approved']),
              delivered: _int(progress['delivered']),
            )
          : const MachineProgress(),
      canStartInspection: _bool(json['canStartInspection']),
      canDeliver: _bool(json['canDeliver']),
      machines: machines,
      canReview: canReview,
      canApprove: canApprove,
      reviewBlockers: [
        if (json['reviewBlockers'] is List)
          for (final blocker in json['reviewBlockers'] as List)
            if (_blocker(blocker) case final text?) text,
      ],
      reviewHistory: [
        if (json['reviewHistory'] is List)
          for (final entry in json['reviewHistory'] as List)
            if (_comment(entry) case final comment?) comment,
      ],
    );
  }

  static MachineForm machineForm(Map<String, dynamic> json) {
    final header = json['header'];
    final answers = json['answers'];

    return MachineForm(
      requestId: _string(json['requestId']),
      requestItemId: _string(json['requestItemId']),
      requestStatus: _string(json['requestStatus']),
      status: MachineStatus.fromValue(json['status']),
      canEdit: _bool(json['canEdit']),
      canReview: _bool(json['canReview']),
      readyToSubmit: _bool(json['readyToSubmit']),
      missing: [
        if (json['missing'] is List) ...(json['missing'] as List).whereType<String>(),
      ],
      templateKey: _nullableString(json['templateKey']),
      templateName: _string(json['templateName']),
      header: header is Map<String, dynamic>
          ? MachineFormHeader(
              clientName: _string(header['clientName']),
              clientAddress: _string(header['clientAddress']),
              clientRepresentative: _string(header['clientRepresentative']),
              inspectedItem: _string(header['inspectedItem']),
              standards: [
                if (header['standards'] is List) ...(header['standards'] as List).whereType<String>(),
              ],
              lastExaminationDate: _nullableString(header['lastExaminationDate']),
              nextExaminationDue: _nullableString(header['nextExaminationDue']),
              inspectorName: _string(header['inspectorName']),
            )
          : const MachineFormHeader(),
      fields: [
        for (final field in _maps(json['fields']))
          MachineFormField(
            key: _string(field['key']),
            label: _string(field['label']),
            value: _string(field['value']),
          ),
      ],
      checklist: [
        for (final section in _maps(json['checklist']))
          MachineFormSection(
            key: _string(section['key']),
            title: _string(section['title']),
            items: [
              for (final row in _maps(section['items']))
                MachineFormRow(
                  key: _string(row['key']),
                  label: _string(row['label']),
                  value: _string(row['value']),
                  note: _nullableString(row['note']),
                ),
            ],
          ),
      ],
      answers: answers is Map
          ? {
              for (final entry in answers.entries)
                if (entry.value is bool) entry.key.toString(): entry.value as bool,
            }
          : const {},
      texts: _stringMap(json['texts']),
      dates: _stringMap(json['dates']),
      reviewNote: _nullableString(json['reviewNote']),
      certificateNumber: _nullableString(json['certificateNumber']),
    );
  }
}
