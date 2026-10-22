import 'package:intl/intl.dart';
import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/domain/entities/user.dart';

/// One inspector choice on the "Choose inspector" step: a radio-style row
/// with their initials, name, qualifications, and a "Best match" tag when
/// [isBestMatch] (their qualifications cover the job's equipment). An
/// inspector on leave shows instead — greyed out, not selectable — like
/// the mockup's disabled "On leave until ..." option.
class InspectorOptionTile extends StatelessWidget {
  final AppUser inspector;
  final bool selected;
  final bool isBestMatch;
  final VoidCallback onTap;

  const InspectorOptionTile({
    super.key,
    required this.inspector,
    required this.selected,
    required this.isBestMatch,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final onLeave = inspector.isOnLeave();

    return Semantics(
      button: true,
      selected: selected,
      enabled: !onLeave,
      child: GestureDetector(
        onTap: onLeave ? null : onTap,
        behavior: HitTestBehavior.opaque,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 160),
          padding: EdgeInsets.symmetric(horizontal: selected ? 13 : 14, vertical: selected ? 11 : 12),
          decoration: BoxDecoration(
            color: onLeave
                ? AppColours.surfaceMuted
                : selected
                ? AppColours.primaryTint
                : Colors.white,
            borderRadius: BorderRadius.circular(14),
            border: Border.all(
              color: selected ? AppColours.primaryColor : AppColours.border,
              width: selected ? 2 : 1,
            ),
          ),
          child: Row(
            children: [
              Icon(
                selected ? Icons.radio_button_checked : Icons.radio_button_off,
                size: 20,
                color: onLeave
                    ? AppColours.border
                    : selected
                    ? AppColours.primaryColor
                    : AppColours.inkMuted,
              ),
              const SizedBox(width: 12),
              Container(
                width: 40,
                height: 40,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: onLeave
                      ? AppColours.borderSubtle
                      : selected
                      ? AppColours.navy
                      : AppColours.primaryTint,
                  shape: BoxShape.circle,
                ),
                child: Text(
                  inspector.initials,
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w700,
                    color: onLeave
                        ? AppColours.inkMuted
                        : selected
                        ? Colors.white
                        : AppColours.primaryDark,
                  ),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      inspector.name,
                      style: AppTextStyles.cardTitle.copyWith(color: onLeave ? AppColours.inkMuted : null),
                    ),
                    Text(
                      onLeave
                          ? t.onLeaveUntilLabel(
                              DateFormat.MMMd(Localizations.localeOf(context).languageCode).format(inspector.onLeaveUntil!),
                            )
                          : (inspector.qualifications.isEmpty
                                ? t.noQualificationsListed
                                : inspector.qualifications.map((q) => q.name).join(', ')),
                      style: AppTextStyles.caption.copyWith(height: 18 / 13),
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ),
              ),
              if (isBestMatch && !onLeave) ...[
                const SizedBox(width: 8),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(color: AppColours.navy, borderRadius: BorderRadius.circular(999)),
                  child: Text(
                    t.bestMatchTag,
                    style: AppTextStyles.badge.copyWith(fontSize: 11, color: Colors.white),
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
