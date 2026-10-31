import 'dart:ui' show FontFeature;

import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/utils/currency.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_status.dart';
import '../../domain/entities/quote_board_tab.dart';
import 'quote_board_labels.dart';
import 'quote_board_status_chip.dart';
import 'quote_reply_badge.dart';

/// One row of the Quotations tracker, built to be scanned down a list: the
/// client and the price share the top line (so the amounts line up on the
/// right), the quote number and place sit under the client, and a footer
/// says where the quote stands and what to do next.
///
/// Status is a dot and a word beside the timing rather than a pill, so the
/// default state of the current tab doesn't shout on every row; an exception
/// (draft, expiring, a client reply) takes the colour instead. A client's
/// counter-offer or question replaces the plain price with what was asked.
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
    final replied =
        quotation.status == QuotationStatus.countered && QuoteReplyBadge.appliesTo(quotation);
    final hasCounter = quotation.clientCounterPricePiastres != null;
    final note = quotation.clientResponseNote?.trim();
    final hasNote = !hasCounter && note != null && note.isNotEmpty;
    final price = quotation.totalPiastres;
    // A draft with nothing priced yet would read "EGP 0.00": say nothing
    // instead, the footer says what's missing.
    final showPrice = !hasCounter && price != null && (price > 0 || !quotation.isDraft);

    final reference = [
      quotation.requestNumber.isEmpty ? quotation.id : quotation.requestNumber,
      quotation.equipmentSummary.isNotEmpty ? quotation.equipmentSummary : quotation.location,
    ].where((part) => part.isNotEmpty).join(' · ');

    final action = _action(t, tab, expired);
    final status = _status(t, tab, expired, replied);
    final timing = quotation.isDraft
        ? (quotation.missingPrices > 0 ? t.quoteMissingPrices(quotation.missingPrices) : '')
        : QuoteWaitingLabel.textFor(t, quotation);

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
            padding: const EdgeInsets.fromLTRB(20, 18, 20, 16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: Text(
                        quotation.clientName,
                        style: AppTextStyles.cardTitle,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    if (showPrice) ...[
                      const SizedBox(width: 12),
                      Text(
                        Currency.formatEgp(price!),
                        style: AppTextStyles.cardTitle.copyWith(
                          fontWeight: FontWeight.w700,
                          fontFeatures: const [FontFeature.tabularFigures()],
                        ),
                      ),
                    ],
                  ],
                ),
                if (reference.isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(
                    reference,
                    style: AppTextStyles.caption.copyWith(height: 18 / 13),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
                if (hasCounter) ...[
                  const SizedBox(height: 14),
                  _CounterPrices(t: t, quotation: quotation),
                ] else if (hasNote) ...[
                  const SizedBox(height: 14),
                  _ClientNote(note: note!),
                ],
                const SizedBox(height: 14),
                Row(
                  children: [
                    Expanded(
                      child: _StatusLine(
                        status: status,
                        timing: timing,
                        timingColor: quotation.isExpiringSoon() && !replied
                            ? AppColours.chipAmberText
                            : AppColours.inkMuted,
                      ),
                    ),
                    if (action != null) ...[
                      const SizedBox(width: 12),
                      _ActionButton(
                        label: action.label,
                        // One filled button per list: the reply that's waiting
                        // on the coordinator. The rest stay quiet.
                        primary: action.primary,
                        onPressed: onTap,
                      ),
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

  ({String label, bool primary})? _action(AppLocalizations t, QuoteBoardTab tab, bool expired) {
    if (quotation.isDraft) return (label: t.actionContinueDraft, primary: false);
    return switch (tab) {
      QuoteBoardTab.open when quotation.status == QuotationStatus.countered =>
        (label: t.actionRespond, primary: true),
      QuoteBoardTab.open =>
        quotation.isExpiringSoon() ? (label: t.actionExtendOrResend, primary: false) : null,
      QuoteBoardTab.accepted => (label: t.actionViewJob, primary: false),
      QuoteBoardTab.rejected => expired ? (label: t.actionRequote, primary: true) : null,
    };
  }

  _Status _status(AppLocalizations t, QuoteBoardTab tab, bool expired, bool replied) {
    if (quotation.isDraft) {
      return _Status(AppColours.inkMuted, AppColours.inkMuted, t.quoteDraftLabel);
    }
    if (tab == QuoteBoardTab.rejected && expired) {
      return _Status(AppColours.inkMuted, AppColours.inkMuted, t.quotationExpiredLabel);
    }
    if (replied) {
      return _Status(
        AppColours.primaryColor,
        AppColours.chipBlueText,
        quotation.clientCounterPricePiastres != null ? t.quoteBadgeCounterOffer : t.quoteBadgeQuestion,
      );
    }
    if (quotation.isExpiringSoon()) {
      return _Status(AppColours.chipAmberText, AppColours.chipAmberText, tab.label(t));
    }
    return switch (tab) {
      QuoteBoardTab.open => _Status(AppColours.primaryColor, AppColours.chipBlueText, tab.label(t)),
      QuoteBoardTab.accepted => _Status(AppColours.successIcon, AppColours.chipGreenText, tab.label(t)),
      QuoteBoardTab.rejected => _Status(AppColours.errorIcon, AppColours.chipRedText, tab.label(t)),
    };
  }
}

/// A status as a dot colour, a text colour and a word.
class _Status {
  final Color dot;
  final Color text;
  final String label;

  const _Status(this.dot, this.text, this.label);
}

/// "● Open · Expires in 13 days": the status word with its dot, then the
/// timing. Wraps rather than truncating when the action button leaves little
/// room.
class _StatusLine extends StatelessWidget {
  final _Status status;
  final String timing;
  final Color timingColor;

  const _StatusLine({required this.status, required this.timing, required this.timingColor});

  @override
  Widget build(BuildContext context) {
    return Text.rich(
      TextSpan(
        children: [
          WidgetSpan(
            alignment: PlaceholderAlignment.middle,
            child: Padding(
              padding: const EdgeInsetsDirectional.only(end: 6),
              child: Container(
                width: 8,
                height: 8,
                decoration: BoxDecoration(color: status.dot, shape: BoxShape.circle),
              ),
            ),
          ),
          TextSpan(
            text: status.label,
            style: AppTextStyles.badge.copyWith(color: status.text),
          ),
          if (timing.isNotEmpty)
            TextSpan(
              text: ' · $timing',
              style: AppTextStyles.caption.copyWith(fontSize: 12, height: 16 / 12, color: timingColor),
            ),
        ],
      ),
      maxLines: 2,
      overflow: TextOverflow.ellipsis,
    );
  }
}

/// A counter-offer: what the coordinator asked next to what the client is
/// asking, side by side.
class _CounterPrices extends StatelessWidget {
  final AppLocalizations t;
  final Quotation quotation;

  const _CounterPrices({required this.t, required this.quotation});

  @override
  Widget build(BuildContext context) {
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
}

/// A client's question: their note, quoted.
class _ClientNote extends StatelessWidget {
  final String note;

  const _ClientNote({required this.note});

  @override
  Widget build(BuildContext context) {
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
        maxLines: 3,
        overflow: TextOverflow.ellipsis,
      ),
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
            style: AppTextStyles.cardTitle.copyWith(
              fontSize: 15,
              color: valueColor,
              fontFeatures: const [FontFeature.tabularFigures()],
            ),
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
  }
}

/// The card's next step. Purely a signpost: the whole card already opens
/// the quote on tap. Filled only when the coordinator owes a reply; the rest
/// are tonal so a column of them doesn't compete with the money.
class _ActionButton extends StatelessWidget {
  final String label;
  final bool primary;
  final VoidCallback onPressed;

  const _ActionButton({required this.label, required this.primary, required this.onPressed});

  @override
  Widget build(BuildContext context) {
    final foreground = primary ? Colors.white : AppColours.primaryDark;
    return SizedBox(
      height: 40,
      child: FilledButton(
        onPressed: onPressed,
        style: FilledButton.styleFrom(
          backgroundColor: primary ? AppColours.primaryColor : AppColours.primaryTint,
          foregroundColor: foreground,
          elevation: 0,
          padding: const EdgeInsets.symmetric(horizontal: 16),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        ),
        child: Text(
          label,
          style: AppTextStyles.buttonLabel.copyWith(fontSize: 13, color: foreground),
        ),
      ),
    );
  }
}
