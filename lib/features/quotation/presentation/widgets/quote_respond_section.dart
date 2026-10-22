import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quotation.dart';

/// The quote detail's "Respond to client" panel: the actions available
/// once a version is out — send a new price, accept the client's counter
/// in one tap (when there is one), or log anything else.
class QuoteRespondSection extends StatelessWidget {
  final Quotation current;
  final VoidCallback onSendNewPrice;
  final VoidCallback? onAcceptClientPrice;
  final VoidCallback onLogOtherReply;
  final VoidCallback onMarkDeclined;

  const QuoteRespondSection({
    super.key,
    required this.current,
    required this.onSendNewPrice,
    required this.onAcceptClientPrice,
    required this.onLogOtherReply,
    required this.onMarkDeclined,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final nextVersion = t.quoteVersionLabel((current.version ?? 0) + 1);

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
              onPressed: onSendNewPrice,
              icon: const Icon(Icons.post_add_rounded, size: 18),
              label: Text(t.sendNewPriceVersion(nextVersion)),
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColours.primaryColor,
                foregroundColor: Colors.white,
                elevation: 0,
                textStyle: AppTextStyles.buttonLabel,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
              ),
            ),
          ),
          if (onAcceptClientPrice != null) ...[
            const SizedBox(height: 10),
            SizedBox(
              width: double.infinity,
              height: 54,
              child: OutlinedButton.icon(
                onPressed: onAcceptClientPrice,
                icon: const Icon(Icons.check_rounded, size: 20),
                label: Text(t.acceptClientPrice),
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColours.successIcon,
                  side: const BorderSide(color: AppColours.successBorder),
                  textStyle: AppTextStyles.buttonLabel,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                ),
              ),
            ),
          ],
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: SizedBox(
                  height: 48,
                  child: OutlinedButton(
                    onPressed: onLogOtherReply,
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppColours.ink,
                      side: const BorderSide(color: AppColours.border),
                      textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                    ),
                    child: Text(t.logOtherReply),
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: SizedBox(
                  height: 48,
                  child: OutlinedButton(
                    onPressed: onMarkDeclined,
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppColours.dangerText,
                      side: const BorderSide(color: AppColours.dangerBorder),
                      textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                    ),
                    child: Text(t.markDeclined),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(t.acceptDisclaimer, style: AppTextStyles.caption),
        ],
      ),
    );
  }
}
