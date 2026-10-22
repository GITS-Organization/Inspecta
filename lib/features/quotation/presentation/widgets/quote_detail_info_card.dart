import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quote_board_tab.dart';
import 'quote_board_labels.dart';

/// The quote detail's 2×2 info grid: equipment, request, status, and the
/// current version's validity. Everything shown here already lives on
/// [current], so this doesn't have to wait on the request to load.
class QuoteDetailInfoCard extends StatelessWidget {
  final Quotation current;
  final VoidCallback onOpenRequest;

  const QuoteDetailInfoCard({
    super.key,
    required this.current,
    required this.onOpenRequest,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final dateFormat = DateFormat.yMMMd(Localizations.localeOf(context).languageCode);
    final tab = QuoteBoardTabX.of(current) ?? QuoteBoardTab.open;
    final validUntil = current.validUntil;

    return MCard(
      child: Column(
        children: [
          IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Expanded(
                  child: _Field(label: t.equipmentLabel, value: current.equipmentSummary),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _Field(
                    label: t.requestLabel,
                    value: current.requestNumber,
                    onTap: onOpenRequest,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 8),
          IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Expanded(child: _Field(label: t.statusLabel, value: tab.label(t))),
                const SizedBox(width: 8),
                Expanded(
                  child: _Field(
                    label: t.validUntilLabel(t.quoteVersionLabel(current.version ?? 0)),
                    value: validUntil == null ? '—' : dateFormat.format(validUntil),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Field extends StatelessWidget {
  final String label;
  final String value;
  final VoidCallback? onTap;

  const _Field({required this.label, required this.value, this.onTap});

  @override
  Widget build(BuildContext context) {
    final content = Padding(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            label,
            style: AppTextStyles.caption.copyWith(fontSize: 12, height: 16 / 12),
            overflow: TextOverflow.ellipsis,
          ),
          const SizedBox(height: 3),
          Text(
            value,
            maxLines: 2,
            style: AppTextStyles.emphasis.copyWith(
              fontSize: 14,
              height: 20 / 14,
              color: onTap == null ? AppColours.ink : AppColours.primaryDark,
            ),
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
    return Material(
      color: AppColours.surfaceMuted,
      borderRadius: BorderRadius.circular(14),
      clipBehavior: Clip.antiAlias,
      child: onTap == null ? content : InkWell(onTap: onTap, child: content),
    );
  }
}
