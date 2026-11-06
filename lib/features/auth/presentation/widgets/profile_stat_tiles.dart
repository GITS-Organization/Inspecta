import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';

/// How a stat reads at a glance: [good] for something achieved, [alert] for
/// something that needs the user once it is above zero, [neutral] otherwise.
enum ProfileStatTone { neutral, good, alert }

/// One figure of the Profile screen's "This month" card.
class ProfileStat {
  /// `null` means this role has no data source for the figure yet (its feature
  /// isn't built): it shows as a dash rather than a misleading zero.
  final int? value;
  final String label;
  final IconData icon;
  final ProfileStatTone tone;

  const ProfileStat({
    required this.value,
    required this.label,
    required this.icon,
    this.tone = ProfileStatTone.neutral,
  });
}

/// The Profile screen's "This month" card: the month it counts, and up to
/// three figures, each a coloured icon over a big number and its label.
class ProfileStatTiles extends StatelessWidget {
  final List<ProfileStat> stats;

  const ProfileStatTiles({super.key, required this.stats});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(t.profileThisMonth, style: AppTextStyles.sectionTitle)),
              const SizedBox(width: 12),
              _MonthChip(label: DateFormat('MMMM', locale).format(DateTime.now())),
            ],
          ),
          const SizedBox(height: 16),
          IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                for (var i = 0; i < stats.length; i++) ...[
                  if (i > 0) const SizedBox(width: 10),
                  Expanded(child: _Stat(stat: stats[i])),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// The month the figures count, as a small pill with a calendar.
class _MonthChip extends StatelessWidget {
  final String label;

  const _MonthChip({required this.label});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsetsDirectional.fromSTEB(10, 5, 12, 5),
      decoration: BoxDecoration(
        color: AppColours.surfaceMuted,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: AppColours.borderSubtle),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.calendar_month_outlined, size: 14, color: AppColours.inkMuted),
          const SizedBox(width: 6),
          Text(
            label,
            style: AppTextStyles.badge.copyWith(color: AppColours.inkSecondary),
          ),
        ],
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  final ProfileStat stat;

  const _Stat({required this.stat});

  @override
  Widget build(BuildContext context) {
    final value = stat.value;
    // An alarm only while there is something to act on: a zero is just a zero.
    final alarming = stat.tone == ProfileStatTone.alert && (value ?? 0) > 0;
    final (iconBackground, iconColor) = switch (stat.tone) {
      ProfileStatTone.good => (AppColours.successBackground, AppColours.successIcon),
      ProfileStatTone.alert when alarming => (AppColours.errorBackground, AppColours.errorIcon),
      _ => (AppColours.primaryTint, AppColours.primaryDark),
    };

    return Container(
      padding: const EdgeInsets.fromLTRB(12, 12, 12, 14),
      decoration: BoxDecoration(
        color: AppColours.surfaceMuted,
        borderRadius: BorderRadius.circular(18),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 34,
            height: 34,
            decoration: BoxDecoration(color: iconBackground, borderRadius: BorderRadius.circular(11)),
            child: Icon(stat.icon, size: 18, color: iconColor),
          ),
          const SizedBox(height: 12),
          Text(
            value?.toString() ?? '—',
            style: AppTextStyles.pageTitle.copyWith(
              fontSize: 30,
              height: 1.1,
              color: alarming ? AppColours.errorText : AppColours.navy,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            stat.label,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.caption.copyWith(
              fontSize: 12,
              height: 16 / 12,
              fontWeight: FontWeight.w600,
              color: AppColours.inkSecondary,
            ),
          ),
        ],
      ),
    );
  }
}
