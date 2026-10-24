import 'package:flutter/material.dart';

import '../consts/app_colors.dart';
import '../consts/app_text_styles.dart';

/// A pill filter with an optional live count, for the scrollable chip rows
/// under a screen header (Quotations, the coordinator queue).
class AppFilterChip extends StatelessWidget {
  final String label;
  final int count;
  final bool selected;

  /// Red count: something here needs the coordinator.
  final bool alert;

  /// Amber styling, for a cross-cutting filter such as "Expiring soon".
  final bool warning;
  final VoidCallback onTap;

  const AppFilterChip({
    super.key,
    required this.label,
    required this.count,
    required this.selected,
    required this.onTap,
    this.alert = false,
    this.warning = false,
  });

  @override
  Widget build(BuildContext context) {
    final Color background;
    final Color foreground;
    final Color border;
    if (warning) {
      background = selected
          ? AppColours.chipAmberText
          : AppColours.chipAmberBackground;
      foreground = selected ? Colors.white : AppColours.chipAmberText;
      border = selected
          ? AppColours.chipAmberText
          : AppColours.chipAmberBackground;
    } else {
      background = selected ? AppColours.primaryColor : Colors.white;
      foreground = selected ? Colors.white : AppColours.ink;
      border = selected ? AppColours.primaryColor : AppColours.border;
    }

    final Color countBackground;
    final Color countForeground;
    if (selected) {
      countBackground = Colors.white.withValues(alpha: 0.2);
      countForeground = Colors.white;
    } else if (alert && count > 0) {
      countBackground = AppColours.errorIcon;
      countForeground = Colors.white;
    } else if (warning) {
      countBackground = Colors.white.withValues(alpha: 0.7);
      countForeground = AppColours.chipAmberText;
    } else {
      countBackground = AppColours.primaryTint;
      countForeground = AppColours.primaryDark;
    }

    return Semantics(
      button: true,
      selected: selected,
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          height: 40,
          padding: const EdgeInsets.symmetric(horizontal: 14),
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: background,
            borderRadius: BorderRadius.circular(999),
            border: Border.all(color: border),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                label,
                style: AppTextStyles.caption.copyWith(
                  fontSize: 13,
                  height: 1,
                  fontWeight: FontWeight.w600,
                  color: foreground,
                ),
              ),
              if (count > 0) ...[
                const SizedBox(width: 8),
                Container(
                  height: 20,
                  constraints: const BoxConstraints(minWidth: 20),
                  padding: const EdgeInsets.symmetric(horizontal: 6),
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: countBackground,
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    '$count',
                    // A fixed line height keeps the pill round instead of the
                    // font's leading stretching it into an oval.
                    style: AppTextStyles.badge.copyWith(
                      fontSize: 11,
                      height: 1,
                      fontWeight: FontWeight.w700,
                      color: countForeground,
                    ),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
