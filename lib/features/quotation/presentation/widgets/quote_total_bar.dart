import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';

/// The fixed bottom bar on the request/quotation screen: the running
/// total, then Save draft and Send quotation.
class QuoteTotalBar extends StatelessWidget {
  final String unitsLabel;
  final String totalText;
  final bool loading;
  final VoidCallback? onSaveDraft;
  final VoidCallback? onSend;

  const QuoteTotalBar({
    super.key,
    required this.unitsLabel,
    required this.totalText,
    required this.onSaveDraft,
    required this.onSend,
    this.loading = false,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return SafeArea(
      top: false,
      minimum: const EdgeInsets.only(bottom: 6),
      child: Container(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 16),
        decoration: const BoxDecoration(
          color: Colors.white,
          border: Border(top: BorderSide(color: AppColours.borderSubtle)),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              crossAxisAlignment: CrossAxisAlignment.baseline,
              textBaseline: TextBaseline.alphabetic,
              children: [
                Text(
                  t.totalForUnits(unitsLabel),
                  style: AppTextStyles.subtitle,
                ),
                Text(
                  totalText,
                  style: AppTextStyles.cardTitle.copyWith(
                    fontSize: 22,
                    height: 28 / 22,
                    fontWeight: FontWeight.w700,
                    letterSpacing: -0.22,
                    color: AppColours.navy,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                OutlinedButton(
                  onPressed: onSaveDraft,
                  style: OutlinedButton.styleFrom(
                    minimumSize: const Size(0, 54),
                    padding: const EdgeInsets.symmetric(horizontal: 20),
                    foregroundColor: AppColours.ink,
                    side: const BorderSide(color: AppColours.border),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(14),
                    ),
                  ),
                  child: Text(
                    t.saveDraft,
                    style: AppTextStyles.buttonLabel.copyWith(
                      color: onSaveDraft == null
                          ? AppColours.inkMuted
                          : AppColours.ink,
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: SizedBox(
                    height: 54,
                    child: FilledButton(
                      onPressed: onSend,
                      style: FilledButton.styleFrom(
                        backgroundColor: AppColours.primaryColor,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(14),
                        ),
                      ),
                      child: loading
                          ? const SizedBox.square(
                              dimension: 20,
                              child: CircularProgressIndicator(
                                strokeWidth: 2.5,
                                color: Colors.white,
                              ),
                            )
                          : Text(
                              t.sendQuotation,
                              style: AppTextStyles.buttonLabel,
                            ),
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
