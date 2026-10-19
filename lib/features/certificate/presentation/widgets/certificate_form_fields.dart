import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/widgets/app_date_picker.dart';
import '../../../../l10n/app_localizations.dart';
import '../bloc/certificate_cubit.dart';

/// A labelled text field bound to one of the certificate's free-text
/// fields ([CertText]). Typing updates the cubit locally; the draft is
/// only autosaved once the field loses focus (or the screen is left).
class CertificateTextInput extends StatefulWidget {
  final String textKey;
  final String label;
  final String? hint;
  final bool multiline;

  /// Shown under the field in the danger colour — a required-field nudge.
  final String? helper;

  const CertificateTextInput({
    super.key,
    required this.textKey,
    required this.label,
    this.hint,
    this.multiline = false,
    this.helper,
  });

  @override
  State<CertificateTextInput> createState() => _CertificateTextInputState();
}

class _CertificateTextInputState extends State<CertificateTextInput> {
  late final CertificateCubit _cubit;
  late final TextEditingController _controller;
  final _focusNode = FocusNode();

  @override
  void initState() {
    super.initState();
    _cubit = context.read<CertificateCubit>();
    _controller = TextEditingController(text: _cubit.state.certificate?.texts[widget.textKey] ?? '');
    _focusNode.addListener(_persistOnBlur);
  }

  void _persistOnBlur() {
    if (!_focusNode.hasFocus && !_cubit.isClosed) _cubit.persist();
  }

  @override
  void dispose() {
    // Leaving the screen with the keyboard still up never fires a blur.
    if (_focusNode.hasFocus && !_cubit.isClosed) _cubit.persist();
    _focusNode.removeListener(_persistOnBlur);
    _focusNode.dispose();
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final helper = widget.helper;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(widget.label, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
        const SizedBox(height: 8),
        TextField(
          controller: _controller,
          focusNode: _focusNode,
          onChanged: (text) => _cubit.setText(widget.textKey, text),
          onTapOutside: (_) => _focusNode.unfocus(),
          minLines: widget.multiline ? 2 : 1,
          maxLines: widget.multiline ? 5 : 1,
          keyboardType: widget.multiline ? TextInputType.multiline : TextInputType.text,
          textCapitalization: TextCapitalization.sentences,
          cursorColor: AppColours.primaryColor,
          style: AppTextStyles.input.copyWith(fontSize: 15),
          decoration: InputDecoration(
            isDense: true,
            hintText: widget.hint,
            hintStyle: AppTextStyles.input.copyWith(
              fontSize: 15,
              color: AppColours.inkMuted,
              fontWeight: FontWeight.w400,
            ),
            contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
            filled: true,
            fillColor: Colors.white,
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: BorderSide(color: helper == null ? AppColours.border : AppColours.dangerBorder, width: 1.5),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: AppColours.primaryColor, width: 2),
            ),
          ),
        ),
        if (helper != null) ...[
          const SizedBox(height: 6),
          Text(
            helper,
            style: AppTextStyles.caption.copyWith(color: AppColours.dangerText, fontWeight: FontWeight.w700),
          ),
        ],
      ],
    );
  }
}

/// A labelled date field bound to one of the certificate's dates
/// ([CertDate]): tap to pick.
class CertificateDateInput extends StatelessWidget {
  final String dateKey;
  final String label;

  const CertificateDateInput({super.key, required this.dateKey, required this.label});

  Future<void> _pick(BuildContext context, DateTime? current) async {
    final cubit = context.read<CertificateCubit>();
    final picked = await AppDatePicker.pickDate(
      context,
      title: label,
      initialDate: current ?? DateTime.now(),
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );
    if (picked != null) cubit.setDate(dateKey, DateTime(picked.year, picked.month, picked.day));
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    return BlocSelector<CertificateCubit, CertificateState, DateTime?>(
      selector: (state) => state.certificate?.dateOf(dateKey),
      builder: (context, value) {
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(label, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
            const SizedBox(height: 8),
            Material(
              color: Colors.white,
              borderRadius: BorderRadius.circular(12),
              child: InkWell(
                borderRadius: BorderRadius.circular(12),
                onTap: () => _pick(context, value),
                child: Container(
                  padding: const EdgeInsetsDirectional.only(start: 12, end: 6),
                  constraints: const BoxConstraints(minHeight: 50),
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: AppColours.border, width: 1.5),
                  ),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          value == null ? t.selectDateHint : DateFormat('d MMM yyyy', locale).format(value),
                          style: AppTextStyles.input.copyWith(
                            fontSize: 15,
                            color: value == null ? AppColours.inkMuted : null,
                            fontWeight: value == null ? FontWeight.w400 : null,
                          ),
                        ),
                      ),
                      const Padding(
                        padding: EdgeInsets.all(8),
                        child: Icon(Icons.calendar_today_outlined, size: 18, color: AppColours.inkMuted),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}

/// One yes/no question ([CertQuestion]) with a Yes / No toggle.
class CertificateYesNoInput extends StatelessWidget {
  final String question;
  final String label;

  const CertificateYesNoInput({super.key, required this.question, required this.label});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocSelector<CertificateCubit, CertificateState, bool?>(
      selector: (state) => state.certificate?.answerOf(question),
      builder: (context, answer) {
        final cubit = context.read<CertificateCubit>();
        return Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(color: AppColours.surfaceMuted, borderRadius: BorderRadius.circular(14)),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(label, style: AppTextStyles.cardTitle.copyWith(fontSize: 14)),
              const SizedBox(height: 10),
              CertificateSegmented<bool>(
                value: answer,
                options: [
                  CertificateSegmentOption(label: t.yesOption, value: true, selectedColor: AppColours.primaryColor),
                  CertificateSegmentOption(label: t.noOption, value: false, selectedColor: AppColours.primaryColor),
                ],
                onChanged: (value) => cubit.setAnswer(question, value),
              ),
            ],
          ),
        );
      },
    );
  }
}

class CertificateSegmentOption<T> {
  final String label;
  final T value;
  final Color selectedColor;
  final Color selectedTextColor;

  const CertificateSegmentOption({
    required this.label,
    required this.value,
    required this.selectedColor,
    this.selectedTextColor = Colors.white,
  });
}

/// A row of mutually exclusive choices, the selected one filled in its own
/// colour — Pass / Fail / N/A, Yes / No.
class CertificateSegmented<T> extends StatelessWidget {
  final T? value;
  final List<CertificateSegmentOption<T>> options;
  final ValueChanged<T> onChanged;

  const CertificateSegmented({super.key, required this.value, required this.options, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(12)),
      child: Row(
        children: [
          for (var i = 0; i < options.length; i++) ...[
            if (i > 0) const SizedBox(width: 4),
            Expanded(
              child: _Segment(option: options[i], selected: value == options[i].value, onTap: onChanged),
            ),
          ],
        ],
      ),
    );
  }
}

class _Segment<T> extends StatelessWidget {
  final CertificateSegmentOption<T> option;
  final bool selected;
  final ValueChanged<T> onTap;

  const _Segment({required this.option, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected ? option.selectedColor : Colors.transparent,
      borderRadius: BorderRadius.circular(9),
      child: InkWell(
        borderRadius: BorderRadius.circular(9),
        onTap: () => onTap(option.value),
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 10),
          child: Text(
            option.label,
            textAlign: TextAlign.center,
            style: AppTextStyles.badge.copyWith(color: selected ? option.selectedTextColor : AppColours.inkMuted),
          ),
        ),
      ),
    );
  }
}

/// A non-editable label/value pair — what the job already told us
/// (client, location, equipment, certificate number).
class CertificateReadOnlyField extends StatelessWidget {
  final String label;
  final String value;

  const CertificateReadOnlyField({super.key, required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: AppTextStyles.caption),
        const SizedBox(height: 2),
        Text(value.isEmpty ? '—' : value, style: AppTextStyles.cardTitle.copyWith(fontSize: 14)),
      ],
    );
  }
}

/// Spacing between stacked fields inside a section card.
const certificateFieldGap = SizedBox(height: 14);
