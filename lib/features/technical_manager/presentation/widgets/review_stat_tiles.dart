import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';

/// The header's three counts: certificates to review (the navy tile),
/// returned and sent.
class ReviewStatTiles extends StatelessWidget {
  final int toReview;
  final int returned;
  final int sent;

  const ReviewStatTiles({super.key, required this.toReview, required this.returned, required this.sent});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Row(
      children: [
        Expanded(child: _StatTile(value: toReview, label: t.statToReview, emphasised: true)),
        const SizedBox(width: 8),
        Expanded(child: _StatTile(value: returned, label: t.statReturned)),
        const SizedBox(width: 8),
        Expanded(child: _StatTile(value: sent, label: t.profileStatSent)),
      ],
    );
  }
}

class _StatTile extends StatelessWidget {
  final int value;
  final String label;
  final bool emphasised;

  const _StatTile({required this.value, required this.label, this.emphasised = false});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: emphasised ? AppColours.navy : Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: emphasised ? null : Border.all(color: AppColours.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            '$value',
            style: TextStyle(
              fontSize: 24,
              height: 28 / 24,
              fontWeight: FontWeight.w700,
              color: emphasised ? Colors.white : AppColours.ink,
            ),
          ),
          Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.caption.copyWith(
              fontSize: 12,
              height: 16 / 12,
              fontWeight: FontWeight.w600,
              color: emphasised ? AppColours.onNavyMuted : AppColours.inkMuted,
            ),
          ),
        ],
      ),
    );
  }
}
