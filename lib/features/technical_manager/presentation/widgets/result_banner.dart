import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/domain/entities/certificate_result.dart';
import 'review_labels.dart';

/// The inspector's final verdict, with the first recorded defect
/// underneath when there is one.
class ResultBanner extends StatelessWidget {
  final Certificate certificate;

  const ResultBanner({super.key, required this.certificate});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final result = certificate.finalResult;
    if (result == null) return const SizedBox.shrink();
    final detail = certificate.defectSummary;
    final (background, accent) = switch (result) {
      CertificateResult.safeToOperate => (
        AppColours.chipGreenBackground,
        AppColours.successIcon,
      ),
      CertificateResult.safeWithConditions => (
        AppColours.primaryTint,
        AppColours.primaryDark,
      ),
      CertificateResult.notSafe => (
        AppColours.chipRedBackground,
        AppColours.errorIcon,
      ),
    };
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            result == CertificateResult.safeToOperate
                ? Icons.check_circle_outline_rounded
                : Icons.info_outline_rounded,
            size: 22,
            color: accent,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  result.title(t),
                  style: AppTextStyles.cardTitle.copyWith(
                    fontSize: 15,
                    color: accent,
                  ),
                ),
                if (detail != null) ...[
                  const SizedBox(height: 4),
                  Text(
                    detail,
                    style: AppTextStyles.body.copyWith(
                      fontSize: 13,
                      color: AppColours.inkBody,
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}
