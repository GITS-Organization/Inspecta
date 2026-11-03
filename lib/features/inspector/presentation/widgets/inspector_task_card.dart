import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';

/// One task card on the inspector's Tasks tab (Feature 05): the quotation
/// number as the title with the client under it, one meta line (time, site and
/// status), how many of the job's certificates are submitted, and the two
/// things to do — get directions, and carry on with the job. The primary action
/// follows the stage: "Start inspection" until the inspection is under way,
/// then "Continue certificate".
///
/// The day is the heading the card sits under, so the card shows only the time.
class InspectorTaskCard extends StatelessWidget {
  final InspectionRequest task;
  final VoidCallback onOpen;
  final VoidCallback onNavigate;

  const InspectorTaskCard({
    super.key,
    required this.task,
    required this.onOpen,
    required this.onNavigate,
  });

  /// The inspection hasn't begun: the machines screen starts it.
  bool get _notStarted => task.status != JobStatus.inProgress;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final scheduledAt = task.scheduledAt;
    final title = (task.subject?.trim().isNotEmpty ?? false)
        ? task.subject!.trim()
        : task.equipmentTitle;
    final counts = task.certificates;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColours.borderSubtle),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // `Skeleton.leaf` on the coloured parts: while loading they are
              // one grey shape each, not the chip's or tile's own colour.
              Skeleton.leaf(
                child: Container(
                  width: 44,
                  height: 44,
                  decoration: BoxDecoration(
                    color: AppColours.primaryTint,
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: const Icon(
                    Icons.build_outlined,
                    size: 22,
                    color: AppColours.primaryColor,
                  ),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      title,
                      style: AppTextStyles.sectionTitle.copyWith(
                        fontSize: 17,
                        height: 24 / 17,
                      ),
                      overflow: TextOverflow.ellipsis,
                    ),
                    if (task.senderLabel.isNotEmpty)
                      Text(
                        task.senderLabel,
                        style: AppTextStyles.caption.copyWith(
                          height: 18 / 13,
                          color: AppColours.inkSecondary,
                        ),
                        overflow: TextOverflow.ellipsis,
                      ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              if (scheduledAt != null) ...[
                _Meta(
                  icon: Icons.schedule_rounded,
                  label: DateFormat('HH:mm', locale).format(scheduledAt),
                ),
                const SizedBox(width: 16),
              ],
              // The site takes what is left, so the status sits flush with the
              // card's right edge whatever the site's name is.
              Expanded(
                child: task.location.isEmpty
                    ? const SizedBox.shrink()
                    : Align(
                        alignment: AlignmentDirectional.centerStart,
                        child: _Meta(
                          icon: Icons.location_on_outlined,
                          label: task.location,
                        ),
                      ),
              ),
              const SizedBox(width: 8),
              Skeleton.leaf(child: _StatusChip(status: task.status)),
            ],
          ),
          if (counts != null && counts.total > 0) ...[
            const SizedBox(height: 12),
            _CertificateProgress(counts: counts),
          ],
          const SizedBox(height: 12),
          Row(
            children: [
              Skeleton.replace(
                replacement: const Bone(width: 140, height: 52, uniRadius: 16),
                child: SizedBox(
                  height: 52,
                  child: OutlinedButton.icon(
                    onPressed: onNavigate,
                    icon: const Icon(Icons.near_me_outlined, size: 20),
                    label: Text(t.directionsAction),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppColours.ink,
                      side: const BorderSide(color: AppColours.border),
                      textStyle: AppTextStyles.buttonLabel.copyWith(
                        fontSize: 15,
                        letterSpacing: 0,
                        color: AppColours.ink,
                      ),
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(16),
                      ),
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Skeleton.leaf(
                  child: SizedBox(
                    height: 52,
                    child: FilledButton(
                      onPressed: onOpen,
                      style: FilledButton.styleFrom(
                        backgroundColor: AppColours.primaryColor,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(16),
                        ),
                      ),
                      child: FittedBox(
                        fit: BoxFit.scaleDown,
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(
                              _notStarted
                                  ? t.startInspectionAction
                                  : t.continueCertificateAction,
                              style: AppTextStyles.buttonLabel.copyWith(
                                fontSize: 15,
                                letterSpacing: 0,
                              ),
                            ),
                            if (_notStarted) ...[
                              const SizedBox(width: 8),
                              const Icon(Icons.arrow_forward_rounded, size: 20),
                            ],
                          ],
                        ),
                      ),
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

class _Meta extends StatelessWidget {
  final IconData icon;
  final String label;

  const _Meta({required this.icon, required this.label});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 14, color: AppColours.inkMuted),
        const SizedBox(width: 4),
        Flexible(
          child: Text(
            label,
            style: AppTextStyles.caption.copyWith(height: 18 / 13),
            overflow: TextOverflow.ellipsis,
          ),
        ),
      ],
    );
  }
}

/// "Certificates · 0 of 1 submitted" over a bar of the submitted share.
class _CertificateProgress extends StatelessWidget {
  final CertificateCounts counts;

  const _CertificateProgress({required this.counts});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final value = (counts.submitted / counts.total).clamp(0.0, 1.0);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            Text(
              t.certificatesLabel,
              style: AppTextStyles.caption.copyWith(
                fontWeight: FontWeight.w600,
                color: AppColours.inkSecondary,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                t.machinesProgress(counts.submitted, counts.total),
                style: AppTextStyles.caption,
                textAlign: TextAlign.end,
                overflow: TextOverflow.ellipsis,
              ),
            ),
          ],
        ),
        const SizedBox(height: 6),
        Skeleton.replace(
          replacement: const Bone(width: double.infinity, height: 6, uniRadius: 3),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(999),
            child: LinearProgressIndicator(
              value: value,
              minHeight: 6,
              color: AppColours.successIcon,
              backgroundColor: AppColours.border,
            ),
          ),
        ),
      ],
    );
  }
}

class _StatusChip extends StatelessWidget {
  final JobStatus status;

  const _StatusChip({required this.status});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final (background, textColor, label) = switch (status) {
      JobStatus.assigned => (
        AppColours.chipAmberBackground,
        AppColours.chipAmberText,
        t.statusAssigned,
      ),
      JobStatus.taskAccepted => (
        AppColours.chipGreenBackground,
        AppColours.chipGreenText,
        t.taskAcceptedBadge,
      ),
      _ => (
        AppColours.chipBlueBackground,
        AppColours.chipBlueText,
        t.taskInProgressBadge,
      ),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: AppTextStyles.badge.copyWith(
          fontSize: 13,
          height: 16 / 13,
          color: textColor,
        ),
      ),
    );
  }
}
