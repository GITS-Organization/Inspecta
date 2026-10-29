import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quotation.dart';

/// What kind of reply the client left on a countered quote: a new price
/// (a counter-offer) or just a note (a question). Shown instead of the
/// generic status chip on a countered quote's card.
class QuoteReplyBadge extends StatelessWidget {
  final Quotation quotation;

  const QuoteReplyBadge({super.key, required this.quotation});

  static bool appliesTo(Quotation quotation) =>
      quotation.clientCounterPricePiastres != null ||
      (quotation.clientResponseNote?.trim().isNotEmpty ?? false);

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final label = quotation.clientCounterPricePiastres != null
        ? t.quoteBadgeCounterOffer
        : t.quoteBadgeQuestion;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: AppColours.chipBlueBackground,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(label, style: AppTextStyles.badge.copyWith(color: AppColours.chipBlueText)),
    );
  }
}
