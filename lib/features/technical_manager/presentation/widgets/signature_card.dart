import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';
import 'signature_pad.dart';

/// The review's signature: the one saved in the reviewer's profile when they
/// have one — shown, not asked for again — otherwise the signature pad with
/// its Clear button, and a switch to keep what they draw for next time.
class SignatureCard extends StatelessWidget {
  /// The saved signature: empty when there isn't one, null while it loads.
  final List<List<double>>? savedStrokes;
  final List<List<double>> strokes;
  final ValueChanged<List<List<double>>> onStrokesChanged;
  final VoidCallback onClear;
  final bool rememberSignature;
  final ValueChanged<bool> onRememberChanged;
  final bool enabled;

  const SignatureCard({
    super.key,
    required this.savedStrokes,
    required this.strokes,
    required this.onStrokesChanged,
    required this.onClear,
    required this.rememberSignature,
    required this.onRememberChanged,
    this.enabled = true,
  });

  bool get _hasSaved => savedStrokes?.any((stroke) => stroke.length >= 4) ?? false;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          if (savedStrokes == null)
            const SizedBox(height: 120, child: Center(child: CircularProgressIndicator(strokeWidth: 2.5)))
          else if (_hasSaved)
            ..._saved(t)
          else
            ..._pad(t),
        ],
      ),
    );
  }

  List<Widget> _saved(AppLocalizations t) => [
    Text(t.savedSignatureTitle, style: AppTextStyles.sectionTitle),
    const SizedBox(height: 16),
    Container(
      height: 110,
      width: double.infinity,
      decoration: BoxDecoration(
        color: AppColours.surfaceMuted,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColours.border),
      ),
      child: SignaturePreview(strokes: savedStrokes!),
    ),
    const SizedBox(height: 10),
    Text(t.savedSignatureHint, style: AppTextStyles.caption),
  ];

  List<Widget> _pad(AppLocalizations t) => [
    Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(t.signHereTitle, style: AppTextStyles.sectionTitle),
        TextButton(
          onPressed: enabled && strokes.isNotEmpty ? onClear : null,
          style: TextButton.styleFrom(
            backgroundColor: AppColours.primaryTint,
            foregroundColor: AppColours.primaryDark,
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
          ),
          child: Text(t.clearAction, style: const TextStyle(fontWeight: FontWeight.w700)),
        ),
      ],
    ),
    const SizedBox(height: 16),
    SizedBox(
      height: 160,
      child: SignaturePad(strokes: strokes, onChanged: onStrokesChanged, enabled: enabled),
    ),
    const SizedBox(height: 8),
    Row(
      children: [
        Expanded(child: Text(t.rememberSignatureLabel, style: AppTextStyles.emphasis.copyWith(fontSize: 14))),
        Switch(
          value: rememberSignature,
          activeTrackColor: AppColours.primaryColor,
          onChanged: enabled ? onRememberChanged : null,
        ),
      ],
    ),
  ];
}
