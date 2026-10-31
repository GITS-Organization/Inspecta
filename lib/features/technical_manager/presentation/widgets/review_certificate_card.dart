import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import 'review_labels.dart';

/// One job with certificates waiting on the technical manager's signature:
/// cert number, equipment and client, who inspected it and when it was last
/// updated, and the "Review & sign" action that opens its machines.
class ReviewCertificateCard extends StatelessWidget {
  final InspectionRequest request;
  final String? inspectorName;
  final VoidCallback onOpen;

  const ReviewCertificateCard({super.key, required this.request, required this.onOpen, this.inspectorName});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final submittedAt = request.updatedAt;
    final counts = request.certificates;
    final name = inspectorName;
    final when = submittedAt == null ? null : reviewWhenLabel(t, submittedAt, locale);
    final subtitle = switch ((name, when)) {
      (final String n, final String w) => t.byInspectorWhenLabel(n, w),
      (final String n, null) => n,
      (null, final String w) => w,
      _ => '',
    };
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            certNumberFor(request),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.caption.copyWith(
              fontWeight: FontWeight.w600,
              letterSpacing: 0.24,
              height: 16 / 12,
              fontSize: 12,
            ),
          ),
          const SizedBox(height: 12),
          Text('${request.equipmentTitle} — ${request.clientName}', style: AppTextStyles.cardTitle),
          if (subtitle.isNotEmpty) ...[
            const SizedBox(height: 2),
            Text(subtitle, style: AppTextStyles.caption.copyWith(height: 18 / 13)),
          ],
          if (counts != null && counts.total > 0) ...[
            const SizedBox(height: 8),
            Text(
              t.certificatesSubmittedProgress(counts.submitted, counts.total),
              style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w600, height: 18 / 13),
            ),
          ],
          const SizedBox(height: 16),
          SizedBox(
            width: double.infinity,
            height: 48,
            child: FilledButton(
              onPressed: onOpen,
              style: FilledButton.styleFrom(
                backgroundColor: AppColours.primaryColor,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
              ),
              child: Text(t.reviewAndSignAction, style: AppTextStyles.buttonLabel),
            ),
          ),
        ],
      ),
    );
  }
}
