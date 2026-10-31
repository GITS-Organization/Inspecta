import '../../domain/entities/machine_form.dart';

/// The body of `PUT /quotation-queue/{id}/machines/{itemId}`: only what
/// changed; parts left empty are left out.
class SaveMachineBody {
  final MachineFormChanges changes;

  const SaveMachineBody(this.changes);

  Map<String, dynamic> toJson() => {
    if (changes.fields.isNotEmpty) 'fields': changes.fields,
    if (changes.checklist.isNotEmpty)
      'checklist': {
        for (final entry in changes.checklist.entries)
          entry.key: {
            'value': entry.value.value,
            if (entry.value.note != null) 'note': entry.value.note,
          },
      },
    if (changes.answers.isNotEmpty) 'answers': changes.answers,
    if (changes.texts.isNotEmpty) 'texts': changes.texts,
    if (changes.dates.isNotEmpty) 'dates': changes.dates,
  };
}
