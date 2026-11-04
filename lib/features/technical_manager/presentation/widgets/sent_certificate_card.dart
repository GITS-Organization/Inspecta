import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../requests/domain/entities/inspection_request.dart';

/// One job whose certificates are already signed — sent to its client, or
/// approved and with the coordinator to send: certificate number and where it
/// stands, the client and quotation, who inspected where and when, and a
/// button to view the PDF of each machine's certificate.
class SentCertificateCard extends StatelessWidget {
  final InspectionRequest request;
  final VoidCallback onOpen;

  const SentCertificateCard({super.key, required this.request, required this.onOpen});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final quotation = request.equipmentTitle;
    final inspector = request.inspectorName;
    final inspectedAt = request.scheduledAt?.toLocal();

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: AppColours.borderSubtle),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: Text(
                    certNumberFor(request),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppTextStyles.caption.copyWith(
                      fontFamily: 'monospace',
                      fontFamilyFallback: const ['Courier New', 'Courier'],
                      fontWeight: FontWeight.w700,
                      letterSpacing: 0.4,
                      height: 16 / 12,
                      fontSize: 12,
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 12),
              _StatusPill(delivered: request.status == JobStatus.sentToClient),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            request.senderLabel,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.sectionTitle.copyWith(fontWeight: FontWeight.w700),
          ),
          if (quotation.isNotEmpty && quotation != request.senderLabel) ...[
            const SizedBox(height: 2),
            Text(quotation, maxLines: 1, overflow: TextOverflow.ellipsis, style: AppTextStyles.subtitle),
          ],
          if ((inspector != null && inspector.isNotEmpty) || request.location.isNotEmpty) ...[
            const SizedBox(height: 14),
            Wrap(
              spacing: 16,
              runSpacing: 8,
              children: [
                if (inspector != null && inspector.isNotEmpty)
                  _MetaItem(icon: Icons.person_outline_rounded, label: inspector),
                if (request.location.isNotEmpty)
                  _MetaItem(icon: Icons.location_on_outlined, label: request.location),
              ],
            ),
          ],
          if (inspectedAt != null) ...[
            const SizedBox(height: 8),
            _MetaItem(
              icon: Icons.access_time_sharp,
              label: t.sentInspectedOn(DateFormat('EEE, d MMM · HH:mm', locale).format(inspectedAt)),
            ),
          ],
          const SizedBox(height: 16),
          SizedBox(
            width: double.infinity,
            height: 48,
            child: OutlinedButton.icon(
              onPressed: onOpen,
              icon: const Icon(Icons.description_outlined, size: 20),
              label: Text(t.viewPdfAction),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColours.ink,
                side: const BorderSide(color: AppColours.border),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                textStyle: AppTextStyles.fieldLabel.copyWith(fontSize: 15, fontWeight: FontWeight.w700),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// "Delivered" once the client has the certificates; "With coordinator" while
/// they're approved and waiting to be sent.
class _StatusPill extends StatelessWidget {
  final bool delivered;

  const _StatusPill({required this.delivered});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final background = delivered ? AppColours.chipGreenBackground : AppColours.chipAmberBackground;
    final foreground = delivered ? AppColours.chipGreenText : AppColours.chipAmberText;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(999)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(delivered ? Icons.check_rounded : Icons.schedule_rounded, size: 14, color: foreground),
          const SizedBox(width: 4),
          Text(
            delivered ? t.statDelivered : t.sentStatusWithCoordinator,
            style: AppTextStyles.badge.copyWith(color: foreground),
          ),
        ],
      ),
    );
  }
}

/// An icon and one short line of the card's details.
class _MetaItem extends StatelessWidget {
  final IconData icon;
  final String label;

  const _MetaItem({required this.icon, required this.label});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 16, color: AppColours.inkMuted),
        const SizedBox(width: 6),
        Flexible(
          child: Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.caption.copyWith(color: AppColours.inkSecondary, height: 1.3),
          ),
        ),
      ],
    );
  }
}
