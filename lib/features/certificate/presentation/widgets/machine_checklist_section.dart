import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../machines/domain/entities/machine_form.dart';
import '../../domain/machine_certificate_mapper.dart';
import '../bloc/certificate_cubit.dart';
import 'certificate_form_fields.dart';
import 'certificate_section_card.dart';

/// The machine's checklist (machine mode only): each section's rows ticked
/// Yes / No / N/A, and a note on every No, all saved to the API as they are
/// answered.
class MachineChecklistSection extends StatelessWidget {
  const MachineChecklistSection({super.key});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return BlocBuilder<CertificateCubit, CertificateState>(
      buildWhen: (previous, current) => previous.checklist != current.checklist,
      builder: (context, state) {
        if (state.checklist.isEmpty) return const SizedBox.shrink();

        final rows = [for (final section in state.checklist) ...section.items];
        final answered = rows.where((row) => MachineCertificateMapper.normalizeAnswer(row.value).isNotEmpty).length;

        return CertificateSectionCard(
          stepNumber: 3,
          highlighted: false,
          done: answered == rows.length,
          title: t.machineChecklistTitle,
          subtitle: t.machineChecklistProgress(answered, rows.length),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              for (var i = 0; i < state.checklist.length; i++) ...[
                if (i > 0) const SizedBox(height: 18),
                Text(state.checklist[i].title, style: AppTextStyles.cardTitle.copyWith(fontSize: 14)),
                const SizedBox(height: 10),
                for (var j = 0; j < state.checklist[i].items.length; j++) ...[
                  if (j > 0) const SizedBox(height: 12),
                  _ChecklistRow(key: ValueKey(state.checklist[i].items[j].key), row: state.checklist[i].items[j]),
                ],
              ],
            ],
          ),
        );
      },
    );
  }
}

class _ChecklistRow extends StatelessWidget {
  final MachineFormRow row;

  const _ChecklistRow({super.key, required this.row});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<CertificateCubit>();
    final answer = MachineCertificateMapper.normalizeAnswer(row.value);

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(color: AppColours.surfaceMuted, borderRadius: BorderRadius.circular(14)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(row.label, style: AppTextStyles.cardTitle.copyWith(fontSize: 14)),
          const SizedBox(height: 10),
          CertificateSegmented<String>(
            value: answer.isEmpty ? null : answer,
            options: [
              CertificateSegmentOption(label: t.yesOption, value: 'yes', selectedColor: AppColours.successIcon),
              CertificateSegmentOption(label: t.noOption, value: 'no', selectedColor: AppColours.errorIcon),
              CertificateSegmentOption(label: t.naOption, value: 'na', selectedColor: AppColours.inkMuted),
            ],
            onChanged: (value) => cubit.setChecklistAnswer(row.key, value),
          ),
          if (answer == 'no') ...[
            const SizedBox(height: 10),
            _NoteInput(initialValue: row.note ?? '', label: t.noteLabel, rowKey: row.key),
          ],
        ],
      ),
    );
  }
}

/// The note a "No" needs: typing updates the row locally, and the answer is
/// saved once the field loses focus (or the screen is left with it focused).
class _NoteInput extends StatefulWidget {
  final String initialValue;
  final String label;
  final String rowKey;

  const _NoteInput({required this.initialValue, required this.label, required this.rowKey});

  @override
  State<_NoteInput> createState() => _NoteInputState();
}

class _NoteInputState extends State<_NoteInput> {
  late final CertificateCubit _cubit = context.read<CertificateCubit>();
  late final TextEditingController _controller = TextEditingController(text: widget.initialValue);
  final _focusNode = FocusNode();

  @override
  void initState() {
    super.initState();
    _focusNode.addListener(_persistOnBlur);
  }

  void _persistOnBlur() {
    if (!_focusNode.hasFocus && !_cubit.isClosed) _cubit.persist();
  }

  @override
  void dispose() {
    if (_focusNode.hasFocus && !_cubit.isClosed) _cubit.persist();
    _focusNode.removeListener(_persistOnBlur);
    _focusNode.dispose();
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(widget.label, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
        const SizedBox(height: 8),
        TextField(
          controller: _controller,
          focusNode: _focusNode,
          onChanged: (note) => _cubit.setChecklistNote(widget.rowKey, note),
          onTapOutside: (_) => _focusNode.unfocus(),
          minLines: 2,
          maxLines: 5,
          keyboardType: TextInputType.multiline,
          textCapitalization: TextCapitalization.sentences,
          cursorColor: AppColours.primaryColor,
          style: AppTextStyles.input.copyWith(fontSize: 15),
          decoration: InputDecoration(
            isDense: true,
            contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
            filled: true,
            fillColor: Colors.white,
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: AppColours.border, width: 1.5),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: AppColours.primaryColor, width: 2),
            ),
          ),
        ),
      ],
    );
  }
}

/// The gap under the checklist card — only there when the card is.
class MachineChecklistGap extends StatelessWidget {
  const MachineChecklistGap({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocSelector<CertificateCubit, CertificateState, bool>(
      selector: (state) => state.checklist.isNotEmpty,
      builder: (context, hasChecklist) => hasChecklist ? const SizedBox(height: 14) : const SizedBox.shrink(),
    );
  }
}
