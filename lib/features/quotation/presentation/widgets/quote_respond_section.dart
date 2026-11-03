import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quotation.dart';

/// The quote detail's "Respond to client" panel: what the coordinator can do
/// once a version is out, whether the client has answered or not.
///
/// - change the offer: reopen it as a new revision with the same prices (for
///   changes after a phone call or a reply), or send a new price straight away
///   (a counter offer);
/// - accept it, or reject the request;
/// - ignore the client's reply, when there is one and it needs no action.
class QuoteRespondSection extends StatelessWidget {
  final Quotation current;
  final bool busy;
  final VoidCallback onReopen;
  final VoidCallback onSendNewPrice;
  final VoidCallback onAccept;
  final VoidCallback onReject;

  /// Ignores the client's reply; null when there is none.
  final VoidCallback? onDismissReply;

  const QuoteRespondSection({
    super.key,
    required this.current,
    required this.busy,
    required this.onReopen,
    required this.onSendNewPrice,
    required this.onAccept,
    required this.onReject,
    required this.onDismissReply,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final nextVersion = t.quoteVersionLabel((current.version ?? 0) + 1);
    final shape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(14));

    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(t.respondToClientTitle, style: AppTextStyles.sectionTitle),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            height: 54,
            child: ElevatedButton.icon(
              onPressed: busy ? null : onReopen,
              icon: const Icon(Icons.edit_note_rounded, size: 20),
              label: Text(t.reopenQuotation),
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColours.primaryColor,
                foregroundColor: Colors.white,
                disabledBackgroundColor: AppColours.primaryColor.withValues(alpha: 0.5),
                disabledForegroundColor: Colors.white70,
                elevation: 0,
                textStyle: AppTextStyles.buttonLabel,
                shape: shape,
              ),
            ),
          ),
          const SizedBox(height: 6),
          Text(t.reopenQuotationHint, style: AppTextStyles.caption),
          const SizedBox(height: 10),
          // SizedBox(
          //   width: double.infinity,
          //   height: 48,
          //   child: OutlinedButton.icon(
          //     onPressed: busy ? null : onSendNewPrice,
          //     icon: const Icon(Icons.post_add_rounded, size: 18),
          //     label: Text(t.sendNewPriceVersion(nextVersion)),
          //     style: OutlinedButton.styleFrom(
          //       foregroundColor: AppColours.ink,
          //       side: const BorderSide(color: AppColours.border),
          //       textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14),
          //       shape: shape,
          //     ),
          //   ),
          // ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: SizedBox(
                  height: 48,
                  child: OutlinedButton.icon(
                    onPressed: busy ? null : onAccept,
                    icon: const Icon(Icons.check_rounded, size: 18),
                    label: Text(t.acceptOfferAction, maxLines: 1, overflow: TextOverflow.ellipsis),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppColours.successIcon,
                      side: const BorderSide(color: AppColours.successBorder),
                      textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14),
                      padding: const EdgeInsets.symmetric(horizontal: 10),
                      shape: shape,
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: SizedBox(
                  height: 48,
                  child: OutlinedButton.icon(
                    onPressed: busy ? null : onReject,
                    icon: const Icon(Icons.close_rounded, size: 18),
                    label: Text(t.rejectRequestAction, maxLines: 1, overflow: TextOverflow.ellipsis),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppColours.dangerText,
                      side: const BorderSide(color: AppColours.dangerBorder),
                      textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14),
                      padding: const EdgeInsets.symmetric(horizontal: 10),
                      shape: shape,
                    ),
                  ),
                ),
              ),
            ],
          ),
          if (onDismissReply != null) ...[
            const SizedBox(height: 10),
            SizedBox(
              width: double.infinity,
              height: 48,
              child: TextButton.icon(
                onPressed: busy ? null : onDismissReply,
                icon: const Icon(Icons.mark_email_read_outlined, size: 18),
                label: Text(t.dismissReply),
                style: TextButton.styleFrom(
                  foregroundColor: AppColours.inkBody,
                  textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14),
                  shape: shape,
                ),
              ),
            ),
            Center(child: Text(t.dismissReplyHint, style: AppTextStyles.caption, textAlign: TextAlign.center)),
          ],
          const SizedBox(height: 10),
          Text(t.acceptDisclaimer, style: AppTextStyles.caption),
        ],
      ),
    );
  }
}
