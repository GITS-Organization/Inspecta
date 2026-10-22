import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';

/// The Profile screen's "This month" card: up to three stat tiles. A
/// `null` value means this role has no data source for that tile yet
/// (its feature isn't built) — it renders as a placeholder dash rather
/// than a misleading zero.
class ProfileStatTiles extends StatelessWidget {
  final List<(int? value, String label)> stats;

  const ProfileStatTiles({super.key, required this.stats});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(t.profileThisMonth, style: AppTextStyles.sectionTitle),
          const SizedBox(height: 16),
          IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                for (var i = 0; i < stats.length; i++) ...[
                  if (i > 0) const SizedBox(width: 8),
                  Expanded(child: _Stat(value: stats[i].$1, label: stats[i].$2)),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  final int? value;
  final String label;

  const _Stat({required this.value, required this.label});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
      decoration: BoxDecoration(
        color: AppColours.surfaceMuted,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            value?.toString() ?? '—',
            style: AppTextStyles.pageTitle.copyWith(fontSize: 28, height: 32 / 28, color: AppColours.navy),
          ),
          const SizedBox(height: 4),
          Text(
            label,
            maxLines: 2,
            style: AppTextStyles.caption.copyWith(fontSize: 12, height: 16 / 12, fontWeight: FontWeight.w600),
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
  }
}
