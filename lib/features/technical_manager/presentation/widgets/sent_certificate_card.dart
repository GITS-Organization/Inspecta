import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/domain/usecases/watch_certificate.dart';
import '../../../certificate/presentation/pdf/certificate_pdf_preview.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import 'review_labels.dart';

/// One certificate already signed — sent to its client, or approved and with
/// the coordinator to send: verdict badge, equipment and
/// client, who it went to, and a link to its PDF.
class SentCertificateCard extends StatelessWidget {
  final InspectionRequest request;

  const SentCertificateCard({super.key, required this.request});

  Future<void> _viewPdf(BuildContext context, Certificate? certificate) async {
    final t = AppLocalizations.of(context)!;
    if (certificate == null) {
      MToast.showError(message: t.certificateNotFound);
      return;
    }
    await previewCertificatePdf(context, request: request, certificate: certificate);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return StreamBuilder<Certificate?>(
      stream: getIt<WatchCertificate>()(request.id),
      builder: (context, snapshot) {
        final certificate = snapshot.data;
        final result = certificate?.finalResult;
        final sentTo = certificate?.sentTo;
        return Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(20),
            boxShadow: [
              BoxShadow(color: AppColours.ink.withValues(alpha: 0.05), blurRadius: 16, offset: const Offset(0, 6)),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  if (result != null)
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                      decoration: BoxDecoration(color: result.badgeBackground, borderRadius: BorderRadius.circular(999)),
                      child: Text(result.badgeLabel(t), style: AppTextStyles.badge.copyWith(color: result.badgeText)),
                    )
                  else
                    const SizedBox.shrink(),
                  Text(
                    certNumberFor(request),
                    style: const TextStyle(fontFamily: 'monospace', fontSize: 12, color: AppColours.inkMuted),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Text('${request.equipmentTitle} — ${request.clientName}', style: AppTextStyles.cardTitle.copyWith(fontSize: 17)),
              if (request.status == JobStatus.certificateApproved) ...[
                const SizedBox(height: 2),
                Text(t.awaitingCoordinatorLabel, style: AppTextStyles.subtitle.copyWith(fontSize: 13)),
              ] else if (sentTo != null && sentTo.isNotEmpty) ...[
                const SizedBox(height: 2),
                Text(t.sentToLabel(sentTo), style: AppTextStyles.subtitle.copyWith(fontSize: 13)),
              ],
              const SizedBox(height: 10),
              Align(
                alignment: AlignmentDirectional.centerEnd,
                child: GestureDetector(
                  onTap: () => _viewPdf(context, certificate),
                  child: Text(t.viewPdfAction, style: AppTextStyles.link.copyWith(fontSize: 13)),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}
