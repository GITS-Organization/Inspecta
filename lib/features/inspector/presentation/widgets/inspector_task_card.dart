import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';

/// One task card on the inspector's Tasks tab (Feature 05): the job's time,
/// equipment and site, with "Continue certificate" and a way to navigate to
/// the site.
///
/// Today's tasks show just their time; [showDate] adds the day (upcoming and
/// overdue ones), and a positive [overdueDays] flags how late it is.
class InspectorTaskCard extends StatelessWidget {
  final InspectionRequest task;
  final bool showDate;
  final int overdueDays;
  final VoidCallback onContinueCertificate;
  final VoidCallback onNavigate;

  const InspectorTaskCard({
    super.key,
    required this.task,
    this.showDate = false,
    this.overdueDays = 0,
    required this.onContinueCertificate,
    required this.onNavigate,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final scheduledAt = task.scheduledAt;

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
              Expanded(
                child: Text(
                  scheduledAt == null
                      ? '—'
                      : DateFormat(showDate ? 'EEE, d MMM · HH:mm' : 'HH:mm', locale).format(scheduledAt),
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.sectionTitle.copyWith(fontSize: 20, height: 26 / 20, fontWeight: FontWeight.w700),
                ),
              ),
              const SizedBox(width: 8),
              _StatusBadge(status: task.status),
            ],
          ),
          if (overdueDays > 0) ...[
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
              decoration: BoxDecoration(color: AppColours.chipRedBackground, borderRadius: BorderRadius.circular(999)),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.schedule_rounded, size: 14, color: AppColours.chipRedText),
                  const SizedBox(width: 4),
                  Text(
                    t.taskOverdueByDays(overdueDays),
                    style: AppTextStyles.badge.copyWith(color: AppColours.chipRedText),
                  ),
                ],
              ),
            ),
          ],
          const SizedBox(height: 16),
          Row(
            children: [
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(color: AppColours.primaryTint, borderRadius: BorderRadius.circular(14)),
                child: const Icon(Icons.precision_manufacturing_outlined, size: 22, color: AppColours.primaryColor),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(task.equipmentTitle, style: AppTextStyles.cardTitle, overflow: TextOverflow.ellipsis),
                    const SizedBox(height: 2),
                    Row(
                      children: [
                        const Icon(Icons.location_on_outlined, size: 15, color: AppColours.inkMuted),
                        const SizedBox(width: 4),
                        Expanded(
                          child: Text(
                            task.location,
                            style: AppTextStyles.caption.copyWith(height: 18 / 13),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Row(
              children: [
                SizedBox(
                  width: 48,
                  height: 48,
                  child: OutlinedButton(
                    onPressed: onNavigate,
                    style: OutlinedButton.styleFrom(
                      padding: EdgeInsets.zero,
                      foregroundColor: AppColours.ink,
                      side: const BorderSide(color: AppColours.border),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                    ),
                    child: const Icon(Icons.near_me_outlined, size: 18),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: SizedBox(
                    height: 48,
                    child: FilledButton(
                      onPressed: onContinueCertificate,
                      style: FilledButton.styleFrom(
                        backgroundColor: AppColours.primaryColor,
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                      ),
                      child: Text(
                        t.continueCertificateAction,
                        style: AppTextStyles.buttonLabel.copyWith(fontSize: 14),
                      ),
                    ),
                  ),
                ),
              ],
            ),
        ],
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  final JobStatus status;

  const _StatusBadge({required this.status});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final (background, textColor, label) = switch (status) {
      JobStatus.assigned => (AppColours.chipAmberBackground, AppColours.chipAmberText, t.statusAssigned),
      JobStatus.taskAccepted => (AppColours.chipGreenBackground, AppColours.chipGreenText, t.taskAcceptedBadge),
      _ => (AppColours.chipBlueBackground, AppColours.chipBlueText, t.taskInProgressBadge),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(999)),
      child: Text(label, style: AppTextStyles.badge.copyWith(color: textColor)),
    );
  }
}
