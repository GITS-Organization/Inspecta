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

  /// The first of [keys] that [json] holds as a non-empty string.
  static String? _pick(Map json, List<String> keys) {
    for (final key in keys) {
      final value = _nullableString(json[key]);
      if (value != null) return value;
    }
    return null;
  }

  /// One certificate of the delivery email. The PDF link names the machine
  /// (`…/machines/{itemId}/pdf`), which stands in for an id the API left out.
  static DeliveryCertificate _deliveryCertificate(Map<String, dynamic> json) {
    final pdfUrl = _pick(json, ['pdfUrl', 'pdf']);
    return DeliveryCertificate(
      requestItemId:
          _pick(json, ['requestItemId', 'itemId']) ??
          (pdfUrl == null ? null : RegExp(r'/machines/([^/?#]+)/pdf').firstMatch(pdfUrl)?.group(1)),
      number: _pick(json, ['number', 'certificateNumber']) ?? '',
      name: _pick(json, ['name', 'machineName', 'itemName']),
      result: _pick(json, ['result']),
      examinationDate: _pick(json, ['examinationDate', 'examinedAt', 'date']),
      nextExaminationDate: _pick(json, ['nextExaminationDate', 'nextExaminationDue']),
      checkUrl: _pick(json, ['checkUrl', 'verifyUrl', 'verificationUrl', 'qrUrl']),
      pdfUrl: pdfUrl,
    );
  }

  static DeliveryPreview deliveryPreview(Map<String, dynamic> json) {
    final to = json['to'];
    final certificates = _maps(json['certificates']).map(_deliveryCertificate).toList();
    final blockers = [
      if (json['blockers'] is List)
        for (final blocker in json['blockers'] as List)
          if (_blocker(blocker) case final text?) text,
    ];
    return DeliveryPreview(
      // `to` is an address, or a list of them: the first one is the client's.
      to: to is List ? to.whereType<String>().where((a) => a.isNotEmpty).firstOrNull : _nullableString(to),
      cc: _strings(json['cc']),
      subject: _string(json['subject']),
      body: _string(json['body'] ?? json['text']),
      certificates: certificates,
      // Without a flag, the email can go unless the API names a blocker.
      canSend: json['canSend'] is bool ? json['canSend'] as bool : blockers.isEmpty,
      blockers: blockers,
    );
  }

  /// A `delivery` object: what went out to the client. Null when [value]
  /// isn't one.
  static DeliveryResult? _delivery(Object? value) {
    if (value is! Map) return null;
    final sentAt = value['sentAt'];
    return DeliveryResult(
      emailSent: value['emailSent'] == true,
      sentTo: _pick(value, ['sentTo', 'to']),
      cc: _strings(value['cc']),
      subject: _pick(value, ['subject']),
      sentAt: sentAt is String ? DateTime.tryParse(sentAt) : null,
    );
  }

  static DeliveryResult deliveryResult(Object? json) =>
      _delivery(json is Map ? json['delivery'] : null) ?? const DeliveryResult();

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
          kind: _nullableString(machine['kind']) ?? _nullableString(machine['itemType']),
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

    // The newer answers wrap the quotation work in `request`.
    final request = json['request'] is Map ? json['request'] as Map : const {};

    return JobMachines(
      requestId: _string(json['requestId']),
      requestStatus: _string(json['requestStatus']),
      clientName: _pick(json, ['clientName']) ?? _pick(request, ['clientName']) ?? '',
      location: _pick(json, ['location']) ?? _pick(request, ['location']) ?? '',
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
      delivery: _delivery(json['delivery']),
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
      result: _nullableString(json['result']),
    );
  }
}
