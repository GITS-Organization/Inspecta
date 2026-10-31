import 'package:equatable/equatable.dart';

import 'machine_status.dart';

/// The top of a machine's form: who it is for and what is being examined.
class MachineFormHeader extends Equatable {
  final String clientName;
  final String clientAddress;
  final String clientRepresentative;
  final String inspectedItem;
  final List<String> standards;

  /// `yyyy-MM-dd`, as the API sends it.
  final String? lastExaminationDate;
  final String? nextExaminationDue;
  final String inspectorName;

  const MachineFormHeader({
    this.clientName = '',
    this.clientAddress = '',
    this.clientRepresentative = '',
    this.inspectedItem = '',
    this.standards = const [],
    this.lastExaminationDate,
    this.nextExaminationDue,
    this.inspectorName = '',
  });

  @override
  List<Object?> get props => [
    clientName,
    clientAddress,
    clientRepresentative,
    inspectedItem,
    standards,
    lastExaminationDate,
    nextExaminationDue,
    inspectorName,
  ];
}

/// A technical field of the machine, already filled where known.
class MachineFormField extends Equatable {
  final String key;
  final String label;
  final String value;

  const MachineFormField({required this.key, required this.label, required this.value});

  @override
  List<Object?> get props => [key, label, value];
}

/// A checklist row: [value] is the saved answer (`yes` / `no` / `na`, or empty
/// when unanswered).
class MachineFormRow extends Equatable {
  final String key;
  final String label;
  final String value;
  final String? note;

  const MachineFormRow({required this.key, required this.label, required this.value, this.note});

  @override
  List<Object?> get props => [key, label, value, note];
}

class MachineFormSection extends Equatable {
  final String key;
  final String title;
  final List<MachineFormRow> items;

  const MachineFormSection({required this.key, required this.title, required this.items});

  @override
  List<Object?> get props => [key, title, items];
}

/// One machine's whole form, in the state the API holds it, with what the
/// current user may do now.
class MachineForm extends Equatable {
  final String requestId;
  final String requestItemId;
  final String requestStatus;
  final MachineStatus status;
  final bool canEdit;
  final bool canReview;

  /// True when nothing is missing for a submit; [missing] says what still
  /// stops one.
  final bool readyToSubmit;
  final List<String> missing;
  final String? templateKey;
  final String templateName;
  final MachineFormHeader header;
  final List<MachineFormField> fields;
  final List<MachineFormSection> checklist;
  final Map<String, bool> answers;
  final Map<String, String> texts;

  /// Values are `yyyy-MM-dd`.
  final Map<String, String> dates;

  /// The technical manager's note after a return.
  final String? reviewNote;
  final String? certificateNumber;

  const MachineForm({
    required this.requestId,
    required this.requestItemId,
    required this.requestStatus,
    required this.status,
    required this.header,
    this.canEdit = false,
    this.canReview = false,
    this.readyToSubmit = false,
    this.missing = const [],
    this.templateKey,
    this.templateName = '',
    this.fields = const [],
    this.checklist = const [],
    this.answers = const {},
    this.texts = const {},
    this.dates = const {},
    this.reviewNote,
    this.certificateNumber,
  });

  MachineForm copyWith({
    MachineStatus? status,
    bool? canEdit,
    bool? readyToSubmit,
    List<String>? missing,
    List<MachineFormField>? fields,
    List<MachineFormSection>? checklist,
    Map<String, bool>? answers,
    Map<String, String>? texts,
    Map<String, String>? dates,
    String? reviewNote,
    String? certificateNumber,
  }) {
    return MachineForm(
      requestId: requestId,
      requestItemId: requestItemId,
      requestStatus: requestStatus,
      status: status ?? this.status,
      header: header,
      canEdit: canEdit ?? this.canEdit,
      canReview: canReview,
      readyToSubmit: readyToSubmit ?? this.readyToSubmit,
      missing: missing ?? this.missing,
      templateKey: templateKey,
      templateName: templateName,
      fields: fields ?? this.fields,
      checklist: checklist ?? this.checklist,
      answers: answers ?? this.answers,
      texts: texts ?? this.texts,
      dates: dates ?? this.dates,
      reviewNote: reviewNote ?? this.reviewNote,
      certificateNumber: certificateNumber ?? this.certificateNumber,
    );
  }

  @override
  List<Object?> get props => [
    requestId,
    requestItemId,
    requestStatus,
    status,
    canEdit,
    canReview,
    readyToSubmit,
    missing,
    templateKey,
    templateName,
    header,
    fields,
    checklist,
    answers,
    texts,
    dates,
    reviewNote,
    certificateNumber,
  ];
}

/// What the inspector changed since the last save — sent as a partial update
/// the API merges with what it holds. Dates are `yyyy-MM-dd`.
class MachineFormChanges extends Equatable {
  final Map<String, String> fields;

  /// Checklist row key → answer (`yes` / `no` / `na`) and its note; a `no`
  /// needs a note.
  final Map<String, ({String value, String? note})> checklist;
  final Map<String, bool> answers;
  final Map<String, String> texts;
  final Map<String, String> dates;

  const MachineFormChanges({
    this.fields = const {},
    this.checklist = const {},
    this.answers = const {},
    this.texts = const {},
    this.dates = const {},
  });

  bool get isEmpty =>
      fields.isEmpty && checklist.isEmpty && answers.isEmpty && texts.isEmpty && dates.isEmpty;

  @override
  List<Object?> get props => [fields, checklist, answers, texts, dates];
}
