import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/presentation/widgets/status_chip.dart';

/// One row on the Certificates list: the job, where its certificate stands
/// and — for a job still being worked on or sent back — a way into its
/// machines.
class CertificateListCard extends StatelessWidget {
  final InspectionRequest request;

  /// Opens the job's machines; `null` for a job that is already out of the
  /// inspector's hands.
  final VoidCallback? onOpen;

  const CertificateListCard({super.key, required this.request, this.onOpen});

  /// Still the inspector's to fill in or fix.
  static bool isOpenWork(JobStatus status) =>
      status == JobStatus.inProgress || status == JobStatus.certificateReturned;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final returned = request.status == JobStatus.certificateReturned;
    final open = isOpenWork(request.status);

    final card = Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        border: returned ? Border.all(color: AppColours.chipAmberTextStrong, width: 1.5) : null,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Flexible(
                child: Text(
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
              ),
              const SizedBox(width: 8),
              StatusChip(status: request.status),
            ],
          ),
          const SizedBox(height: 12),
          Text(request.equipmentTitle, style: AppTextStyles.cardTitle, overflow: TextOverflow.ellipsis),
          const SizedBox(height: 2),
          Text(
            [request.clientName, request.location].where((s) => s.isNotEmpty).join(' · '),
            style: AppTextStyles.caption.copyWith(height: 18 / 13),
            overflow: TextOverflow.ellipsis,
          ),
          if (open) ...[
            const SizedBox(height: 16),
            SizedBox(
              height: 48,
              width: double.infinity,
              child: FilledButton(
                onPressed: onOpen,
                style: FilledButton.styleFrom(
                  backgroundColor: AppColours.primaryColor,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                ),
                child: Text(t.continueCertificateAction, style: AppTextStyles.buttonLabel.copyWith(fontSize: 14)),
              ),
            ),
          ],
        ],
      ),
    );

    if (!open || onOpen == null) return card;
    return Material(
      color: Colors.transparent,
      borderRadius: BorderRadius.circular(24),
      child: InkWell(borderRadius: BorderRadius.circular(24), onTap: onOpen, child: card),
    );
  }
}
