import 'package:flutter/material.dart';

import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../requests/domain/entities/inspection_request.dart';

/// One job whose certificates are already signed — sent to its client, or
/// approved and with the coordinator to send: equipment and client, where it
/// stands, and a link to view the PDF of each machine's certificate.
class SentCertificateCard extends StatelessWidget {
  final InspectionRequest request;
  final VoidCallback onOpen;

  const SentCertificateCard({super.key, required this.request, required this.onOpen});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
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
          if (request.status == JobStatus.certificateApproved) ...[
            const SizedBox(height: 2),
            Text(t.awaitingCoordinatorLabel, style: AppTextStyles.caption.copyWith(height: 18 / 13)),
          ],
          const SizedBox(height: 12),
          Align(
            alignment: AlignmentDirectional.centerEnd,
            child: GestureDetector(
              onTap: onOpen,
              child: Text(t.viewPdfAction, style: AppTextStyles.link.copyWith(fontSize: 13)),
            ),
          ),
        ],
      ),
    );
  }
}
