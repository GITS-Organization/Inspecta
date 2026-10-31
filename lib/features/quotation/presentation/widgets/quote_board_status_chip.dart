import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/utils/relative_time.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_status.dart';
import '../../domain/entities/quote_board_tab.dart';
import 'quote_board_labels.dart';

/// The status chip on a board card: coloured by [QuoteBoardTab], amber
/// when a sent quote is within 2 days of expiring, grey once expired.
class QuoteBoardStatusChip extends StatelessWidget {
  final Quotation quotation;

  const QuoteBoardStatusChip({super.key, required this.quotation});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final tab = QuoteBoardTabX.of(quotation) ?? QuoteBoardTab.open;
    final expiringSoon = quotation.isExpiringSoon();
    final expired = tab == QuoteBoardTab.rejected && quotation.isExpired();

    final Color background;
    final Color text;
    final String label;
    if (quotation.isDraft) {
      background = AppColours.chipGreyBackground;
      text = AppColours.chipGreyText;
      label = t.quoteDraftLabel;
    } else if (expired) {
      background = AppColours.chipGreyBackground;
      text = AppColours.chipGreyText;
      label = t.quotationExpiredLabel;
    } else if (expiringSoon) {
      background = AppColours.chipAmberBackground;
      text = AppColours.chipAmberText;
      label = tab.label(t);
    } else {
      (background, text) = switch (tab) {
        QuoteBoardTab.open => (AppColours.chipBlueBackground, AppColours.chipBlueText),
        QuoteBoardTab.accepted => (AppColours.chipGreenBackground, AppColours.chipGreenText),
        QuoteBoardTab.rejected => (AppColours.chipRedBackground, AppColours.chipRedText),
      };
      label = tab.label(t);
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(999)),
      child: Text(label, style: AppTextStyles.badge.copyWith(color: text)),
    );
  }
}

/// "Replied 2h ago" / "Sent 3 days ago" / "Expires in 2 days" / "Expired
/// 5 days ago" — the timing line under a board card.
class QuoteWaitingLabel extends StatelessWidget {
  final Quotation quotation;

  const QuoteWaitingLabel({super.key, required this.quotation});

  /// The timing sentence itself, empty when there's nothing to say — the
  /// board card sets it beside its status.
  static String textFor(AppLocalizations t, Quotation quotation, {DateTime? now}) {
    final at = now ?? DateTime.now();
    if (quotation.status == QuotationStatus.countered &&
        quotation.clientRespondedAt != null) {
      return t.repliedAgo(RelativeDuration.since(quotation.clientRespondedAt!, now: at).label(t));
    } else if (quotation.isExpired(now: at)) {
      return t.expiredLabel(RelativeDuration.since(quotation.validUntil!, now: at).label(t));
    } else if (quotation.validUntil != null &&
        (quotation.status == QuotationStatus.sent ||
            quotation.status == QuotationStatus.countered)) {
      final days = quotation.validUntil!.difference(at).inDays;
      return t.expiresIn(days.clamp(0, 1 << 30));
    } else if (quotation.sentAt != null) {
      return t.sentAgo(RelativeDuration.since(quotation.sentAt!, now: at).label(t));
    }
    return '';
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final text = textFor(t, quotation);

    if (text.isEmpty) return const SizedBox.shrink();
    return Text(
      text,
      style: AppTextStyles.caption.copyWith(fontSize: 12),
      overflow: TextOverflow.ellipsis,
    );
  }
}
