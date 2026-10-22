import 'package:flutter/material.dart';

import '../../l10n/app_localizations.dart';
import '../consts/app_colors.dart';
import 'm_text_field.dart';

/// Password input: an [MTextField] with a lock icon that masks its text and
/// shows an eye button to reveal it.
class MPasswordField extends StatefulWidget {
  final TextEditingController controller;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onSubmitted;
  final bool enabled;
  final String? errorText;
  final String? label;
  final String? hintText;

  /// Light-on-dark styling, as on [MTextField].
  final bool glass;

  const MPasswordField({
    super.key,
    required this.controller,
    this.onChanged,
    this.onSubmitted,
    this.enabled = true,
    this.errorText,
    this.label,
    this.hintText,
    this.glass = false,
  });

  @override
  State<MPasswordField> createState() => _MPasswordFieldState();
}

class _MPasswordFieldState extends State<MPasswordField> {
  bool _obscureText = true;

  void _toggleObscureText() => setState(() => _obscureText = !_obscureText);

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MTextField(
      controller: widget.controller,
      onChanged: widget.onChanged,
      onSubmitted: widget.onSubmitted,
      enabled: widget.enabled,
      errorText: widget.errorText,
      label: widget.label,
      hintText: widget.hintText,
      glass: widget.glass,
      obscureText: _obscureText,
      prefixIcon: Icons.lock_outline_rounded,
      keyboardType: TextInputType.visiblePassword,
      textInputAction: TextInputAction.done,
      autofillHints: const [AutofillHints.password],
      trailing: IconButton(
        onPressed: _toggleObscureText,
        tooltip: _obscureText ? t.showPassword : t.hidePassword,
        color: widget.glass ? Colors.white.withValues(alpha: 0.75) : AppColours.inkSecondary,
        iconSize: 20,
        icon: Icon(
          _obscureText
              ? Icons.visibility_outlined
              : Icons.visibility_off_outlined,
        ),
      ),
    );
  }
}
