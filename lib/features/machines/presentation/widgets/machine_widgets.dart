import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/job_machines.dart';
import '../../domain/entities/machine_status.dart';

/// The banner at the top of the machines screens: a back button, a small
/// [eyebrow] line above the [title].
class MachinesHeader extends StatelessWidget {
  final String eyebrow;
  final String title;

  /// Shown at the end of the row, e.g. a save indicator.
  final Widget? trailing;

  const MachinesHeader({
    super.key,
    required this.eyebrow,
    required this.title,
    this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    return AppBanner(
      topInset: MediaQuery.paddingOf(context).top,
      child: Row(
        children: [
          const MBackButton(),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  eyebrow,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.caption.copyWith(
                    fontWeight: FontWeight.w600,
                    letterSpacing: 0.24,
                    height: 16 / 12,
                    fontSize: 12,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.sectionTitle.copyWith(
                    fontSize: 22,
                    height: 28 / 22,
                    letterSpacing: -0.22,
                  ),
                ),
              ],
            ),
          ),
          if (trailing != null) ...[const SizedBox(width: 8), trailing!],
        ],
      ),
    );
  }
}

/// A machine's certificate status as a coloured pill.
class MachineStatusChip extends StatelessWidget {
  final MachineStatus status;

  const MachineStatusChip({super.key, required this.status});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final (background, textColor, label) = switch (status) {
      MachineStatus.notStarted => (
        AppColours.chipGreyBackground,
        AppColours.chipGreyText,
        t.machineStatusNotStarted,
      ),
      MachineStatus.draft => (
        AppColours.chipAmberBackground,
        AppColours.chipAmberText,
        t.machineStatusDraft,
      ),
      MachineStatus.submitted => (
        AppColours.chipBlueBackground,
        AppColours.chipBlueText,
        t.machineStatusSubmitted,
      ),
      MachineStatus.returned => (
        AppColours.chipRedBackground,
        AppColours.chipRedText,
        t.machineStatusReturned,
      ),
      MachineStatus.approved => (
        AppColours.chipGreenBackground,
        AppColours.chipGreenText,
        t.machineStatusApproved,
      ),
      MachineStatus.delivered => (
        AppColours.chipGreenBackground,
        AppColours.chipGreenText,
        t.machineStatusDelivered,
      ),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(label, style: AppTextStyles.badge.copyWith(color: textColor)),
    );
  }
}

/// One machine of a job, built for the inspector's next move: what the
/// machine is (its name, then the capacity, make, model and serial that
/// identify it), how far its checklist is, any return note or certificate
/// number, and a footer with where the certificate stands and what tapping
/// the card does next.
class MachineCard extends StatelessWidget {
  final MachineSummary machine;
  final VoidCallback onTap;

  /// When set, the card's next step is "View PDF" — for certificates that
  /// are already approved and sent.
  final VoidCallback? onViewPdf;

  /// The inspection hasn't started, so the machine can't be filled in yet:
  /// the footer says so instead of offering a step.
  final bool locked;

  const MachineCard({
    super.key,
    required this.machine,
    required this.onTap,
    this.onViewPdf,
    this.locked = false,
  });

  /// What identifies the machine on site, in the order an inspector reads it.
  String get _facts =>
      [
            machine.capacity,
            machine.manufacturer,
            machine.model,
            machine.serialNumber,
          ]
          .whereType<String>()
          .map((part) => part.trim())
          .where((part) => part.isNotEmpty)
          .join(' · ');

  /// The label of what tapping the card leads to, or null when there's no
  /// step to name.
  String? _next(AppLocalizations t) {
    if (onViewPdf != null) return t.viewPdfAction;
    if (locked) return null;
    if (machine.canReview) return t.machineActionReview;
    return switch (machine.status) {
      MachineStatus.notStarted =>
        machine.canEdit ? t.machineActionFillIn : null,
      MachineStatus.draft =>
        machine.canEdit ? t.actionContinueDraft : t.machineActionView,
      MachineStatus.returned =>
        machine.canEdit ? t.machineActionFixResubmit : t.machineActionView,
      MachineStatus.submitted => t.machineActionView,
      MachineStatus.approved || MachineStatus.delivered => t.viewPdfAction,
    };
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final facts = _facts;
    final next = _next(t);
    final progress = machine.checklistTotal == 0
        ? 0.0
        : (machine.checklistAnswered / machine.checklistTotal).clamp(0.0, 1.0);

    final card = Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
      ),
      clipBehavior: Clip.antiAlias,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(20, 18, 20, 14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: Text(
                        machine.name,
                        style: AppTextStyles.cardTitle,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    if (machine.quantity > 1) ...[
                      const SizedBox(width: 12),
                      Text(
                        t.machineQuantityLabel(machine.quantity),
                        style: AppTextStyles.caption,
                      ),
                    ],
                  ],
                ),
                if (facts.isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(
                    facts,
                    style: AppTextStyles.caption.copyWith(height: 18 / 13),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
                if (machine.checklistTotal > 0) ...[
                  const SizedBox(height: 14),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(999),
                    child: LinearProgressIndicator(
                      value: progress,
                      minHeight: 6,
                      color: AppColours.primaryColor,
                      backgroundColor: AppColours.primaryTint,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    t.machineChecklistProgress(
                      machine.checklistAnswered,
                      machine.checklistTotal,
                    ),
                    style: AppTextStyles.caption,
                  ),
                ],
                if (machine.certificateNumber != null) ...[
                  const SizedBox(height: 10),
                  Text(
                    t.machineCertificateNumber(machine.certificateNumber!),
                    style: AppTextStyles.caption.copyWith(
                      color: AppColours.successText,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
                if (machine.reviewNote != null) ...[
                  const SizedBox(height: 12),
                  ReviewNote(note: machine.reviewNote!),
                ],
                const SizedBox(height: 14),
                Row(
                  children: [
                    MachineStatusChip(status: machine.status),
                    const Spacer(),
                    if (next != null)
                      _NextStep(label: next)
                    else if (locked)
                      Flexible(
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(
                              Icons.lock_outline_rounded,
                              size: 15,
                              color: AppColours.inkMuted,
                            ),
                            const SizedBox(width: 4),
                            Flexible(
                              child: Text(
                                t.machineStartFirstHint,
                                style: AppTextStyles.caption.copyWith(
                                  fontSize: 12,
                                ),
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          ],
                        ),
                      ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
    // A locked card is dimmed: its checklist opens only after the inspection
    // has been started.
    return locked ? Opacity(opacity: 0.6, child: card) : card;
  }
}

/// "Fill in ›": the card's next step as a quiet link with a chevron — the
/// whole card is the tap target, so this only names where it goes.
class _NextStep extends StatelessWidget {
  final String label;

  const _NextStep({required this.label});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: AppTextStyles.link.copyWith(fontSize: 14)),
        const SizedBox(width: 2),
        const Icon(
          Icons.chevron_right_rounded,
          size: 20,
          color: AppColours.primaryColor,
        ),
      ],
    );
  }
}

/// The technical manager's note on a machine that was sent back.
class ReviewNote extends StatelessWidget {
  final String note;

  const ReviewNote({super.key, required this.note});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColours.chipRedBackground,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            t.machineReviewNoteLabel,
            style: AppTextStyles.badge.copyWith(color: AppColours.chipRedText),
          ),
          const SizedBox(height: 4),
          Text(
            note,
            style: AppTextStyles.body.copyWith(color: AppColours.chipRedText),
          ),
        ],
      ),
    );
  }
}
