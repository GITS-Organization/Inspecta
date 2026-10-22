import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/domain/usecases/watch_certificate.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import 'review_labels.dart';

/// One certificate waiting on the technical manager's signature: cert
/// number, verdict badge, equipment and client, who submitted it and when,
/// and the "Review & sign" action (filled when there's a finding to look
/// at, outlined for a clean "safe" one).
class ReviewCertificateCard extends StatelessWidget {
  final InspectionRequest request;
  final String? inspectorName;
  final VoidCallback onOpen;

  const ReviewCertificateCard({super.key, required this.request, required this.onOpen, this.inspectorName});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    return StreamBuilder<Certificate?>(
      stream: getIt<WatchCertificate>()(request.id),
      builder: (context, snapshot) {
        final certificate = snapshot.data;
        final result = certificate?.finalResult;
        final submittedAt = certificate?.submittedAt;
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
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  if (result != null)
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                      decoration: BoxDecoration(color: result.badgeBackground, borderRadius: BorderRadius.circular(999)),
                      child: Text(result.badgeLabel(t), style: AppTextStyles.badge.copyWith(color: result.badgeText)),
                    )
                  else
                    const SizedBox.shrink(),
                  const SizedBox(width: 8),
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
                ],
              ),
              const SizedBox(height: 12),
              Text('${request.equipmentTitle} — ${request.clientName}', style: AppTextStyles.cardTitle),
              if (subtitle.isNotEmpty) ...[
                const SizedBox(height: 2),
                Text(subtitle, style: AppTextStyles.caption.copyWith(height: 18 / 13)),
              ],
              const SizedBox(height: 16),
              SizedBox(
                width: double.infinity,
                height: 48,
                child: result.needsAttention
                    ? FilledButton(
                        onPressed: onOpen,
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColours.primaryColor,
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                        ),
                        child: Text(t.reviewAndSignAction, style: AppTextStyles.buttonLabel),
                      )
                    : OutlinedButton(
                        onPressed: onOpen,
                        style: OutlinedButton.styleFrom(
                          foregroundColor: AppColours.ink,
                          side: const BorderSide(color: AppColours.border),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                        ),
                        child: Text(
                          t.reviewAndSignAction,
                          style: AppTextStyles.buttonLabel.copyWith(color: AppColours.ink),
                        ),
                      ),
              ),
            ],
          ),
        );
      },
    );
  }
}
