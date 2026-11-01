import 'package:flutter/material.dart';

import '../../l10n/app_localizations.dart';
import '../consts/app_colors.dart';
import '../consts/app_text_styles.dart';
import '../shared/m_text_field.dart';
import '../utils/validators.dart';

/// The editable parts of an email to a client: To, Cc (a chip per address, up
/// to [maxCc]), subject and text. Nothing is sent from here: the owner reads
/// the values through the `on…Changed` callbacks and does the sending.
///
/// The fields start from the initial values and keep their own text; to start
/// them over, give the widget a new key.
class EmailComposer extends StatefulWidget {
  /// The most addresses the API takes in Cc.
  static const defaultMaxCc = 10;

  final String to;
  final List<String> cc;
  final String subject;
  final String body;
  final bool enabled;
  final int maxCc;
  final ValueChanged<String> onToChanged;
  final ValueChanged<List<String>> onCcChanged;
  final ValueChanged<String> onSubjectChanged;
  final ValueChanged<String> onBodyChanged;

  const EmailComposer({
    super.key,
    required this.to,
    required this.cc,
    required this.subject,
    required this.body,
    required this.onToChanged,
    required this.onCcChanged,
    required this.onSubjectChanged,
    required this.onBodyChanged,
    this.enabled = true,
    this.maxCc = defaultMaxCc,
  });

  @override
  State<EmailComposer> createState() => _EmailComposerState();
}

class _EmailComposerState extends State<EmailComposer> {
  late final _to = TextEditingController(text: widget.to);
  late final _subject = TextEditingController(text: widget.subject);
  late final _body = TextEditingController(text: widget.body);
  final _ccInput = TextEditingController();
  late List<String> _cc = [...widget.cc];

  /// The To address is only called wrong once the field has been left, not
  /// while it is still being typed.
  bool _toTouched = false;
  bool _toValid = true;
  bool _ccInvalid = false;

  @override
  void initState() {
    super.initState();
    _toValid = widget.to.trim().isEmpty || Validators.isEmail(widget.to);
  }

  @override
  void dispose() {
    _to.dispose();
    _subject.dispose();
    _body.dispose();
    _ccInput.dispose();
    super.dispose();
  }

  /// Adds the address(es) in the Cc input — one, or several separated by
  /// commas, semicolons or spaces. Nothing is added when one of them doesn't
  /// look like an address.
  void _addCc() {
    final entries = _ccInput.text.split(RegExp(r'[,;\s]+')).where((e) => e.isNotEmpty).toList();
    if (entries.isEmpty) return;
    if (!entries.every(Validators.isEmail)) {
      setState(() => _ccInvalid = true);
      return;
    }

    final cc = [..._cc];
    for (final entry in entries) {
      final known = cc.any((a) => a.toLowerCase() == entry.toLowerCase());
      if (!known && cc.length < widget.maxCc) cc.add(entry);
    }
    setState(() {
      _cc = cc;
      _ccInvalid = false;
    });
    _ccInput.clear();
    widget.onCcChanged(cc);
  }

  void _removeCc(String address) {
    setState(() => _cc = [..._cc]..remove(address));
    widget.onCcChanged(_cc);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final enabled = widget.enabled;
    final ccFull = _cc.length >= widget.maxCc;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Focus(
          onFocusChange: (focused) {
            if (!focused && !_toTouched) setState(() => _toTouched = true);
          },
          child: MTextField(
            controller: _to,
            onChanged: (value) {
              setState(() => _toValid = value.trim().isEmpty || Validators.isEmail(value));
              widget.onToChanged(value);
            },
            enabled: enabled,
            label: t.emailToLabel,
            prefixIcon: Icons.mail_outline_rounded,
            keyboardType: TextInputType.emailAddress,
            hintText: t.emailHint,
            errorText: _toTouched && !_toValid ? t.emailInvalidAddress : null,
          ),
        ),
        const SizedBox(height: 16),
        Text(t.emailCcLabel, style: AppTextStyles.badge),
        if (_cc.isNotEmpty) ...[
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final address in _cc)
                InputChip(
                  label: Text(address, style: AppTextStyles.caption),
                  onDeleted: enabled ? () => _removeCc(address) : null,
                  backgroundColor: AppColours.surfaceMuted,
                  side: BorderSide.none,
                ),
            ],
          ),
        ],
        const SizedBox(height: 8),
        MTextField(
          controller: _ccInput,
          onChanged: (_) {
            if (_ccInvalid) setState(() => _ccInvalid = false);
          },
          onSubmitted: (_) => _addCc(),
          enabled: enabled && !ccFull,
          prefixIcon: Icons.person_add_alt_1_outlined,
          keyboardType: TextInputType.emailAddress,
          textInputAction: TextInputAction.done,
          hintText: t.emailCcHint,
          errorText: _ccInvalid ? t.emailInvalidAddress : null,
          trailing: IconButton(
            onPressed: enabled && !ccFull ? _addCc : null,
            iconSize: 22,
            color: AppColours.primaryColor,
            icon: const Icon(Icons.add_rounded),
            tooltip: t.emailCcAddAction,
          ),
        ),
        if (ccFull) ...[
          const SizedBox(height: 6),
          Text(t.emailCcLimit(widget.maxCc), style: AppTextStyles.caption),
        ],
        const SizedBox(height: 16),
        MTextField(
          controller: _subject,
          onChanged: widget.onSubjectChanged,
          enabled: enabled,
          label: t.emailSubjectLabel,
          prefixIcon: Icons.subject_rounded,
          textInputAction: TextInputAction.next,
        ),
        const SizedBox(height: 16),
        Text(t.emailBodyLabel, style: AppTextStyles.badge),
        const SizedBox(height: 8),
        TextField(
          controller: _body,
          onChanged: widget.onBodyChanged,
          enabled: enabled,
          minLines: 8,
          maxLines: 16,
          keyboardType: TextInputType.multiline,
          textInputAction: TextInputAction.newline,
          style: AppTextStyles.body,
          decoration: InputDecoration(
            border: OutlineInputBorder(borderRadius: BorderRadius.circular(14)),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(14),
              borderSide: const BorderSide(color: AppColours.primaryColor, width: 2),
            ),
          ),
        ),
      ],
    );
  }
}
