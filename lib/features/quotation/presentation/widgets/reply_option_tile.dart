import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';

/// One of the three reply choices (BR-03.1): a big radio-style row with a
/// title and a one-line explanation.
class ReplyOptionTile extends StatelessWidget {
  final String title;
  final String subtitle;
  final bool selected;
  final VoidCallback onTap;

  /// Red styling for the Reject option: its title stays red even when
  /// unselected.
  final bool danger;

  const ReplyOptionTile({
    super.key,
    required this.title,
    required this.subtitle,
    required this.selected,
    required this.onTap,
    this.danger = false,
  });

  @override
  Widget build(BuildContext context) {
    final accent = danger ? AppColours.errorIcon : AppColours.primaryColor;
    final titleColor = danger
        ? AppColours.errorText
        : selected
        ? AppColours.primaryDark
        : AppColours.ink;

    return Semantics(
      button: true,
      selected: selected,
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: Container(
          padding: EdgeInsets.symmetric(horizontal: selected ? 13 : 14, vertical: selected ? 11 : 12),
          decoration: BoxDecoration(
            color: selected ? (danger ? AppColours.errorBackground : AppColours.primaryTint) : Colors.white,
            borderRadius: BorderRadius.circular(14),
            border: Border.all(
              color: selected ? accent : AppColours.border,
              width: selected ? 2 : 1,
            ),
          ),
          child: Row(
            children: [
              Icon(
                selected ? Icons.radio_button_checked : Icons.radio_button_off,
                size: 20,
                color: selected ? accent : AppColours.inkMuted,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(title, style: AppTextStyles.cardTitle.copyWith(color: titleColor)),
                    const SizedBox(height: 2),
                    Text(subtitle, style: AppTextStyles.caption.copyWith(height: 18 / 13)),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
