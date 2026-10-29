import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/utils/currency.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_status.dart';
import '../../domain/entities/quote_board_tab.dart';
import 'quote_board_status_chip.dart';
import 'quote_reply_badge.dart';
import 'reply_type_labels.dart';

/// One card on the Quotations tracker: quote number/version, the linked
/// request, client + equipment, price(s) or the client's note, and the
/// one action that status calls for.
class QuoteBoardCard extends StatelessWidget {
  final Quotation quotation;
  final VoidCallback onTap;

  const QuoteBoardCard({
    super.key,
    required this.quotation,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final tab = QuoteBoardTabX.of(quotation) ?? QuoteBoardTab.open;
    final expired = quotation.isExpired();
    final showReplyBadge =
        quotation.status == QuotationStatus.countered && QuoteReplyBadge.appliesTo(quotation);
    final actionLabel = _actionLabel(t, tab, expired);

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
      ),
      clipBehavior: Clip.antiAlias,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: [
                    Expanded(
                      child: Text(
                        // The proposal number already carries the revision.
                        quotation.requestNumber.isEmpty
                            ? quotation.id
                            : quotation.requestNumber,
                        style: AppTextStyles.caption.copyWith(
                          fontWeight: FontWeight.w600,
                          letterSpacing: 0.24,
                          height: 16 / 12,
                          fontSize: 12,
                        ),
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    const SizedBox(width: 8),
                    showReplyBadge
                        ? QuoteReplyBadge(quotation: quotation)
                        : QuoteBoardStatusChip(quotation: quotation),
                  ],
                ),
                const SizedBox(height: 12),
                Text(
                  quotation.clientName,
                  style: AppTextStyles.cardTitle,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 2),
                Text(
                  quotation.equipmentSummary,
                  style: AppTextStyles.caption.copyWith(height: 18 / 13),
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 16),
                _PriceSection(t: t, quotation: quotation),
                const SizedBox(height: 16),
                Row(
                  children: [
                    Expanded(
                      child: QuoteWaitingLabel(quotation: quotation),
                    ),
                    if (actionLabel != null) ...[
                      const SizedBox(width: 8),
                      _ActionButton(label: actionLabel, onPressed: onTap),
                    ],
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  String? _actionLabel(AppLocalizations t, QuoteBoardTab tab, bool expired) {
    if (tab == QuoteBoardTab.lost) {
      return expired ? t.actionRequote : null;
    }
    return switch (tab) {
      QuoteBoardTab.open when quotation.status == QuotationStatus.countered => t.actionRespond,
      QuoteBoardTab.open => quotation.isExpiringSoon() ? t.actionExtendOrResend : null,
      QuoteBoardTab.accepted => t.actionViewJob,
      QuoteBoardTab.lost => null,
    };
  }
}

/// Either the your-price / client-asks pair (a counter-offer), the
/// client's note (a question, no price attached), or the plain price
/// line the other tabs show.
class _PriceSection extends StatelessWidget {
  final AppLocalizations t;
  final Quotation quotation;

  const _PriceSection({required this.t, required this.quotation});

  @override
  Widget build(BuildContext context) {
    if (quotation.clientCounterPricePiastres != null) {
      // IntrinsicHeight gives the Row a bounded height to stretch into —
      // without it, CrossAxisAlignment.stretch demands infinite height
      // inside the card's shrink-wrapped Column and crashes layout.
      return IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Expanded(
              child: _PriceBox(
                label: t.yourPriceLabel,
                price: quotation.totalPiastres,
                background: AppColours.surfaceMuted,
                labelColor: AppColours.inkMuted,
                valueColor: AppColours.ink,
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _PriceBox(
                label: t.clientAsksLabel,
                price: quotation.clientCounterPricePiastres,
                background: AppColours.chipAmberBackground,
                labelColor: AppColours.chipAmberText,
                valueColor: AppColours.chipAmberTextStrong,
              ),
            ),
          ],
        ),
      );
    }

    final note = quotation.clientResponseNote?.trim();
    if (note != null && note.isNotEmpty) {
      return Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        decoration: BoxDecoration(
          color: AppColours.surfaceMuted,
          borderRadius: BorderRadius.circular(14),
        ),
        child: Text(
          '"$note"',
          style: AppTextStyles.subtitle.copyWith(
            fontStyle: FontStyle.italic,
            color: AppColours.inkBody,
          ),
        ),
      );
    }

    return Row(
      children: [
        if (quotation.totalPiastres != null)
          Text(
            Currency.formatEgp(quotation.totalPiastres!),
            style: AppTextStyles.sectionTitle.copyWith(fontWeight: FontWeight.w700),
          )
        else
          Text(quotation.type.label(t), style: AppTextStyles.emphasis),
      ],
    );
  }
}

class _PriceBox extends StatelessWidget {
  final String label;
  final int? price;
  final Color background;
  final Color labelColor;
  final Color valueColor;

  const _PriceBox({
    required this.label,
    required this.price,
    required this.background,
    required this.labelColor,
    required this.valueColor,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(14)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(label, style: AppTextStyles.caption.copyWith(color: labelColor)),
          const SizedBox(height: 2),
          Text(
            price == null ? '—' : Currency.formatEgp(price!),
            style: AppTextStyles.cardTitle.copyWith(fontSize: 15, color: valueColor),
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
  }
}

class _ActionButton extends StatelessWidget {
  final String label;
  final VoidCallback onPressed;

  const _ActionButton({required this.label, required this.onPressed});

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 44,
      child: ElevatedButton(
        onPressed: onPressed,
        style: ElevatedButton.styleFrom(
          backgroundColor: AppColours.primaryColor,
          foregroundColor: Colors.white,
          elevation: 0,
          padding: const EdgeInsets.symmetric(horizontal: 20),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
        ),
        child: Text(label, style: AppTextStyles.buttonLabel.copyWith(fontSize: 14)),
      ),
    );
  }
}
