import 'package:flutter/material.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../core/widgets/banner_art.dart';
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

  /// [AppBannerStyle.ringsCompact] is the inspector's.
  final AppBannerStyle style;

  const MachinesHeader({
    super.key,
    required this.eyebrow,
    required this.title,
    this.trailing,
    this.style = AppBannerStyle.network,
  });

  @override
  Widget build(BuildContext context) {
    final rings = style.isRings;
    // Arabic letters join: tracking would pull them apart.
    final arabic = Localizations.localeOf(context).languageCode == 'ar';
    return AppBanner(
      topInset: MediaQuery.paddingOf(context).top,
      style: style,
      child: Row(
        children: [
          const MBackButton(),
          SizedBox(width: rings ? 14 : 12),
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
                    letterSpacing: rings ? 0 : 0.24,
                    height: rings ? 17 / 13 : 16 / 12,
                    fontSize: rings ? 13 : 12,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.sectionTitle.copyWith(
                    fontSize: rings ? 25 : 22,
                    height: rings ? 1.15 : 28 / 22,
                    fontWeight: rings ? FontWeight.w700 : null,
                    letterSpacing: rings ? (arabic ? 0 : -0.5) : -0.22,
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

  /// A small glyph before the label.
  final IconData? icon;

  const MachineStatusChip({super.key, required this.status, this.icon});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final (background, textColor, label) = switch (status) {
      MachineStatus.notStarted => (
        AppColours.surfaceMuted,
        AppColours.inkSecondary,
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

    final text = Text(
      label,
      style: AppTextStyles.badge.copyWith(fontSize: 13, height: 16 / 13, color: textColor),
    );
    return Container(
      padding: EdgeInsetsDirectional.fromSTEB(icon == null ? 12 : 9, 6, 12, 6),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
        // An idle machine is outlined rather than filled: it is waiting, not
        // flagged.
        border: status == MachineStatus.notStarted ? Border.all(color: AppColours.border) : null,
      ),
      child: icon == null
          ? text
          : Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(icon, size: 15, color: textColor),
                const SizedBox(width: 6),
                text,
              ],
            ),
    );
  }
}

/// One machine of a job, built for the inspector's next move: an icon tile,
/// what the machine is (its name, then the capacity, make and model), where
/// its certificate stands as a chip, tags for what identifies the unit on site
/// (serial number, quantity and the standards), how far its checklist is, any
/// return note or certificate number, and a footer with what tapping the card
/// does next — or, before the inspection has started, one quiet line saying
/// why it can't be opened yet.
///
/// A locked card keeps full contrast: dimming it made it hard to read in
/// sunlight.
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

  static String _join(Iterable<String?> parts, String separator) => parts
      .whereType<String>()
      .map((part) => part.trim())
      .where((part) => part.isNotEmpty)
      .join(separator);

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
    final facts = _join([machine.capacity, machine.manufacturer, machine.model], ' · ');
    final serial = machine.serialNumber?.trim() ?? '';
    final next = _next(t);
    final progress = machine.checklistTotal == 0
        ? 0.0
        : (machine.checklistAnswered / machine.checklistTotal).clamp(0.0, 1.0);
    final tags = [
      if (serial.isNotEmpty) t.machineSerialTag(serial),
      t.machineQtyTag(machine.quantity),
      ...machine.inspectionStandards.map((s) => s.trim()).where((s) => s.isNotEmpty),
    ];

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColours.borderSubtle),
      ),
      clipBehavior: Clip.antiAlias,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // `Skeleton.leaf` on the coloured parts: while loading they
                    // are one grey shape each, not their own colour.
                    Skeleton.leaf(
                      child: Container(
                        width: 44,
                        height: 44,
                        decoration: BoxDecoration(
                          color: AppColours.primaryTint,
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: const Icon(Icons.build_outlined, size: 22, color: AppColours.primaryColor),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            machine.name,
                            style: AppTextStyles.sectionTitle.copyWith(fontSize: 17, height: 24 / 17),
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                          ),
                          if (facts.isNotEmpty)
                            Text(
                              facts,
                              style: AppTextStyles.caption.copyWith(height: 18 / 13, color: AppColours.inkSecondary),
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                            ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 8),
                    Skeleton.leaf(child: MachineStatusChip(status: machine.status)),
                  ],
                ),
                if (tags.isNotEmpty) ...[
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [for (final tag in tags) Skeleton.leaf(child: _Tag(tag))],
                  ),
                ],
                if (machine.checklistTotal > 0) ...[
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Text(
                        t.checklistLabel,
                        style: AppTextStyles.caption.copyWith(
                          fontWeight: FontWeight.w600,
                          color: AppColours.inkSecondary,
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Text(
                          t.machineChecklistProgress(machine.checklistAnswered, machine.checklistTotal),
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
                        value: progress,
                        minHeight: 6,
                        color: AppColours.successIcon,
                        backgroundColor: AppColours.border,
                      ),
                    ),
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
                if (locked) ...[
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      const Icon(Icons.lock_outline_rounded, size: 14, color: AppColours.inkMuted),
                      const SizedBox(width: 6),
                      Flexible(
                        child: Text(
                          t.machineAvailableAfterStart,
                          style: AppTextStyles.caption.copyWith(height: 18 / 13),
                        ),
                      ),
                    ],
                  ),
                ] else if (next != null) ...[
                  const SizedBox(height: 8),
                  Align(
                    alignment: AlignmentDirectional.centerEnd,
                    child: _NextStep(label: next),
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// A small outlined tag: a serial number, a quantity, a standard.
class _Tag extends StatelessWidget {
  final String label;

  const _Tag(this.label);

  @override
  Widget build(BuildContext context) {
    // No alignment or fixed height: either would make the tag fill the row
    // instead of hugging its text.
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: AppColours.surfaceMuted,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: AppColours.borderSubtle),
      ),
      child: Text(
        label,
        style: AppTextStyles.badge.copyWith(fontWeight: FontWeight.w500, color: AppColours.inkSecondary),
      ),
    );
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
