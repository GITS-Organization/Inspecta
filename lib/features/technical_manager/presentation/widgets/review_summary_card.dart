import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_template.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import 'certificate_finding_tiles.dart';
import 'result_banner.dart';

/// What the reviewer is signing off: client, equipment, when it was
/// inspected and when it's next due, the key findings and the verdict.
///
/// [onViewPdf] adds a "View full PDF" link beside the title; leave it null
/// when the screen already has a PDF button of its own.
class ReviewSummaryCard extends StatelessWidget {
  final InspectionRequest request;
  final Certificate certificate;
  final VoidCallback? onViewPdf;

  const ReviewSummaryCard({
    super.key,
    required this.request,
    required this.certificate,
    this.onViewPdf,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    // The dates the inspector put on the certificate; a certificate from
    // before that template falls back to the job's own schedule.
    final inspected =
        certificate.dateOf(CertDate.examination) ??
        request.scheduledAt ??
        request.receivedAt;
    final nextDue =
        certificate.dateOf(CertDate.nextExamination) ??
        DateTime(inspected.year + 1, inspected.month, inspected.day);
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                t.reviewSummaryTitle,
                style: AppTextStyles.sectionTitle.copyWith(fontSize: 14),
              ),
              if (onViewPdf != null)
                GestureDetector(
                  onTap: onViewPdf,
                  child: Text(
                    t.viewFullPdfAction,
                    style: AppTextStyles.link.copyWith(fontSize: 14),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 16),
          IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Expanded(
                  child: _Fact(label: t.clientLabel, value: request.clientName),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _Fact(
                    label: t.equipmentLabel,
                    value: request.equipmentTitle,
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
                Expanded(
                  child: _Fact(
                    label: t.inspectedLabel,
                    value: DateFormat('d MMM yyyy', locale).format(inspected),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _Fact(
                    label: t.nextDueLabel,
                    value: DateFormat('MMM yyyy', locale).format(nextDue),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          CertificateFindingTiles(certificate: certificate),
          const SizedBox(height: 12),
          ResultBanner(certificate: certificate),
        ],
      ),
    );
  }
}

class _Fact extends StatelessWidget {
  final String label;
  final String value;

  const _Fact({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: AppColours.surfaceMuted,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            label,
            style: AppTextStyles.caption.copyWith(
              fontSize: 12,
              height: 16 / 12,
            ),
          ),
          const SizedBox(height: 3),
          Text(
            value,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.emphasis.copyWith(
              fontSize: 14,
              height: 20 / 14,
            ),
          ),
        ],
      ),
    );
  }
}
