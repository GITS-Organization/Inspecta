import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';

/// The reviewer's optional note for the record — a card holding a
/// multi-line box in the same outlined style as `MTextField`, which is
/// single-line only.
class ReviewCommentsField extends StatelessWidget {
  final TextEditingController controller;
  final ValueChanged<String> onChanged;
  final bool enabled;

  const ReviewCommentsField({super.key, required this.controller, required this.onChanged, this.enabled = true});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(t.yourCommentsLabel, style: AppTextStyles.sectionTitle),
          const SizedBox(height: 12),
          TextField(
            controller: controller,
            onChanged: onChanged,
            enabled: enabled,
            minLines: 2,
            maxLines: 4,
            textInputAction: TextInputAction.newline,
            keyboardType: TextInputType.multiline,
            cursorColor: AppColours.primaryColor,
            style: AppTextStyles.input.copyWith(fontSize: 14, height: 20 / 14, fontWeight: FontWeight.w400),
            decoration: InputDecoration(
              hintText: t.reviewCommentsHint,
              hintStyle: AppTextStyles.input.copyWith(
                fontSize: 14,
                color: AppColours.inkMuted,
                fontWeight: FontWeight.w400,
              ),
              filled: true,
              fillColor: Colors.white,
              contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(14),
                borderSide: const BorderSide(color: AppColours.border),
              ),
              disabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(14),
                borderSide: const BorderSide(color: AppColours.border),
              ),
              focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(14),
                borderSide: const BorderSide(color: AppColours.primaryColor, width: 2),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
