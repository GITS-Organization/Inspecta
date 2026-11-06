import 'package:flutter/material.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../machines/domain/entities/job_machines.dart';
import '../../../machines/domain/entities/machine_status.dart';
import '../../../machines/presentation/widgets/machine_widgets.dart';
import 'inspector_design.dart';

/// One machine of a job on the inspector's Items screen, drawn as a ticket.
///
/// Above the perforation is what the machine *is*: an icon tile, its name and
/// kind, where its certificate stands, then capacity / manufacturer / model,
/// the serial number and quantity, and the standards it is inspected to.
/// Below it is what to *do*: how far its checklist is, any return note or
/// certificate number, and the one action that carries on — or, before the
/// inspection has started, one quiet line saying why it can't be opened yet.
///
/// A locked ticket keeps full contrast: dimming made it hard to read in
/// sunlight.
class InspectorMachineTicket extends StatelessWidget {
  final MachineSummary machine;
  final VoidCallback onTap;

  /// When set, the ticket's next step is "View PDF" — for certificates that
  /// are already approved and sent.
  final VoidCallback? onViewPdf;

  /// The inspection hasn't started, so the machine can't be filled in yet.
  final bool locked;

  const InspectorMachineTicket({
    super.key,
    required this.machine,
    required this.onTap,
    this.onViewPdf,
    this.locked = false,
  });

  static const _cardRadius = 26.0;

  /// What the button at the foot says and does, or null when there is no step
  /// to name. [primary] is a step the inspector does; otherwise it only looks.
  ({String label, bool primary, IconData icon})? _next(AppLocalizations t) {
    const go = Icons.arrow_forward_rounded;
    ({String label, bool primary, IconData icon}) view() =>
        (label: t.machineActionView, primary: false, icon: Icons.visibility_outlined);
    ({String label, bool primary, IconData icon}) pdf() =>
        (label: t.viewPdfAction, primary: false, icon: Icons.picture_as_pdf_outlined);

    if (onViewPdf != null) return pdf();
    if (locked) return null;
    if (machine.canReview) return (label: t.machineActionReview, primary: true, icon: go);
    return switch (machine.status) {
      MachineStatus.notStarted =>
        machine.canEdit ? (label: t.machineActionFillIn, primary: true, icon: go) : null,
      MachineStatus.draft =>
        machine.canEdit ? (label: t.actionContinueDraft, primary: true, icon: go) : view(),
      MachineStatus.returned =>
        machine.canEdit ? (label: t.machineActionFixResubmit, primary: true, icon: go) : view(),
      MachineStatus.submitted => view(),
      MachineStatus.approved || MachineStatus.delivered => pdf(),
    };
  }

  static IconData _statusIcon(MachineStatus status) => switch (status) {
    MachineStatus.notStarted => Icons.circle_outlined,
    MachineStatus.draft => Icons.edit_rounded,
    MachineStatus.submitted => Icons.hourglass_top_rounded,
    MachineStatus.returned => Icons.undo_rounded,
    MachineStatus.approved => Icons.verified_outlined,
    MachineStatus.delivered => Icons.mark_email_read_outlined,
  };

  static String _clean(String? value) => value?.trim() ?? '';

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final next = _next(t);

    // What kind of machine it is, unless that only repeats its name.
    final kindOrTemplate = _clean(machine.type).isNotEmpty ? _clean(machine.type) : _clean(machine.templateName);
    final kind = kindOrTemplate.toLowerCase() == _clean(machine.name).toLowerCase() ? '' : kindOrTemplate;
    final specs = [
      (t.capacityLabel, _clean(machine.capacity)),
      (t.manufacturerLabel, _clean(machine.manufacturer)),
      (t.machineModelLabel, _clean(machine.model)),
    ].where((spec) => spec.$2.isNotEmpty).toList();
    final serial = _clean(machine.serialNumber);
    final standards = machine.inspectionStandards.map(_clean).where((s) => s.isNotEmpty).toList();

    // What sits under the perforation, in order.
    final actions = <Widget>[
      if (machine.checklistTotal > 0)
        _ChecklistProgress(answered: machine.checklistAnswered, total: machine.checklistTotal),
      if (machine.certificateNumber != null)
        Text(
          t.machineCertificateNumber(machine.certificateNumber!),
          style: AppTextStyles.caption.copyWith(color: AppColours.successText, fontWeight: FontWeight.w600),
        ),
      if (machine.reviewNote != null) ReviewNote(note: machine.reviewNote!),
      if (locked)
        const _LockedNote()
      else if (next != null)
        InspectorActionButton(
          label: next.label,
          primary: next.primary,
          icon: next.icon,
          onPressed: onViewPdf ?? onTap,
        ),
    ];

    return RiseIn(
      delay: const Duration(milliseconds: 50),
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(_cardRadius),
          boxShadow: inspectorCardShadow,
        ),
        child: Material(
          type: MaterialType.transparency,
          child: InkWell(
            borderRadius: BorderRadius.circular(_cardRadius),
            onTap: onTap,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              mainAxisSize: MainAxisSize.min,
              children: [
                Padding(
                  padding: EdgeInsets.fromLTRB(18, 18, 18, actions.isEmpty ? 18 : 8),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      _Identity(
                        name: machine.name,
                        kind: kind,
                        status: machine.status,
                        statusIcon: _statusIcon(machine.status),
                      ),
                      if (specs.isNotEmpty) ...[
                        const SizedBox(height: 14),
                        _SpecGrid(specs: specs),
                      ],
                      const SizedBox(height: 14),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          if (serial.isNotEmpty) _IdChip(label: t.machineSerialShort, value: serial),
                          _IdChip(label: t.machineQtyShort, value: '${machine.quantity}'),
                        ],
                      ),
                      if (standards.isNotEmpty) ...[
                        const SizedBox(height: 14),
                        _Standards(label: t.machineHeaderStandards, standards: standards),
                      ],
                    ],
                  ),
                ),
                if (actions.isNotEmpty) ...[
                  const TicketPerforation(),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(18, 8, 18, 18),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        for (final (i, action) in actions.indexed) ...[
                          if (i > 0) const SizedBox(height: 12),
                          action,
                        ],
                      ],
                    ),
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

/// The icon tile, the machine's name and kind, and its status.
class _Identity extends StatelessWidget {
  final String name;
  final String kind;
  final MachineStatus status;
  final IconData statusIcon;

  const _Identity({required this.name, required this.kind, required this.status, required this.statusIcon});

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        // `Skeleton.leaf` on the coloured parts: while loading they are one
        // grey shape each, not their own colour.
        Skeleton.leaf(
          child: Container(
            width: 52,
            height: 52,
            decoration: BoxDecoration(color: AppColours.primaryColor, borderRadius: BorderRadius.circular(16)),
            child: const Icon(Icons.precision_manufacturing_outlined, size: 28, color: Colors.white),
          ),
        ),
        const SizedBox(width: 14),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(name, maxLines: 2, overflow: TextOverflow.ellipsis, style: inspectorDisplay(context, 24)),
              if (kind.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.only(top: 3),
                  child: Text(
                    kind,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppTextStyles.subtitle.copyWith(height: 1.3),
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(width: 8),
        Skeleton.leaf(child: MachineStatusChip(status: status, icon: statusIcon)),
      ],
    );
  }
}

/// Capacity, manufacturer and model as equal tiles side by side — only the
/// ones the machine has.
class _SpecGrid extends StatelessWidget {
  final List<(String, String)> specs;

  const _SpecGrid({required this.specs});

  @override
  Widget build(BuildContext context) {
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final (i, spec) in specs.indexed) ...[
            if (i > 0) const SizedBox(width: 8),
            Expanded(
              child: Skeleton.leaf(
                child: Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: AppColours.surfaceMuted,
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        spec.$1,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AppTextStyles.badge.copyWith(fontWeight: FontWeight.w500, color: AppColours.inkMuted),
                      ),
                      const SizedBox(height: 3),
                      Text(
                        spec.$2,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: inspectorDisplay(context, 16, weight: FontWeight.w600, height: 1.25),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// "S/N  N84-4856": an outlined chip, the name muted and the value in mono.
class _IdChip extends StatelessWidget {
  final String label;
  final String value;

  const _IdChip({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Skeleton.leaf(
      child: Container(
        height: 30,
        padding: const EdgeInsets.symmetric(horizontal: 11),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(999),
          border: Border.all(color: AppColours.borderSubtle),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              label,
              style: AppTextStyles.badge.copyWith(color: AppColours.inkMuted),
            ),
            const SizedBox(width: 7),
            Flexible(
              child: Text(
                value,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: inspectorMono.copyWith(fontSize: 13),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Standards extends StatelessWidget {
  final String label;
  final List<String> standards;

  const _Standards({required this.label, required this.standards});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: AppTextStyles.badge.copyWith(fontWeight: FontWeight.w500, color: AppColours.inkMuted)),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            for (final standard in standards)
              Skeleton.leaf(
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  decoration: BoxDecoration(
                    color: AppColours.surfaceMuted,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(standard, style: inspectorMono.copyWith(fontSize: 12)),
                ),
              ),
          ],
        ),
      ],
    );
  }
}

/// How far the machine's checklist is: a ring that sweeps to the answered
/// share and the count beside it. Green once every row is answered, blue
/// while it is being filled in, grey before it is started.
class _ChecklistProgress extends StatelessWidget {
  final int answered;
  final int total;

  const _ChecklistProgress({required this.answered, required this.total});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final progress = (answered / total).clamp(0.0, 1.0);
    final done = answered >= total;

    final Color background;
    final Color border;
    final Color arc;
    final Color track;
    final Color ink;
    final Color sub;
    if (done) {
      background = const Color(0xFFE3F4EA);
      border = const Color(0xFFBCE0CC);
      arc = const Color(0xFF0B6B41);
      track = const Color(0xFFBCE0CC);
      ink = const Color(0xFF0B6B41);
      sub = const Color(0xFF2A4A3A);
    } else if (answered > 0) {
      background = AppColours.primaryTint;
      border = const Color(0xFFCBD9F2);
      arc = AppColours.primaryColor;
      track = const Color(0xFFCBD9F2);
      ink = AppColours.primaryColor;
      sub = AppColours.inkSecondary;
    } else {
      background = AppColours.surfaceMuted;
      border = AppColours.borderSubtle;
      arc = AppColours.inkMuted;
      track = AppColours.borderSubtle;
      ink = AppColours.inkSecondary;
      sub = AppColours.inkMuted;
    }

    final animate = !MediaQuery.disableAnimationsOf(context);
    return Skeleton.leaf(
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: background,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(color: border),
        ),
        child: Row(
          children: [
            TweenAnimationBuilder<double>(
              tween: Tween(begin: animate ? 0 : progress, end: progress),
              duration: animate ? const Duration(milliseconds: 1350) : Duration.zero,
              // The sweep waits 350ms, then takes the last second.
              curve: const Interval(0.26, 1, curve: Cubic(0.4, 0, 0.2, 1)),
              builder: (context, value, _) => SizedBox.square(
                dimension: 56,
                child: CustomPaint(
                  painter: _RingPainter(progress: value, arc: arc, track: track),
                  child: Center(
                    child: done
                        ? Icon(Icons.check_rounded, size: 24, color: arc)
                        : answered > 0
                        ? Text(
                            '${(progress * 100).floor()}%',
                            style: AppTextStyles.badge.copyWith(fontWeight: FontWeight.w700, color: ink),
                          )
                        : null,
                  ),
                ),
              ),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    isArabic(context) ? t.checklistLabel : t.checklistLabel.toUpperCase(),
                    style: AppTextStyles.badge.copyWith(
                      letterSpacing: isArabic(context) ? 0 : 0.48,
                      color: ink,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    t.checklistOfTotal(answered, total),
                    style: inspectorDisplay(context, 22, height: 1.15).copyWith(color: ink),
                  ),
                  const SizedBox(height: 2),
                  Text(t.checklistRowsAnswered, style: AppTextStyles.caption.copyWith(height: 1.3, color: sub)),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _RingPainter extends CustomPainter {
  final double progress;
  final Color arc;
  final Color track;

  const _RingPainter({required this.progress, required this.arc, required this.track});

  @override
  void paint(Canvas canvas, Size size) {
    const stroke = 6.0;
    final rect = Rect.fromCircle(center: size.center(Offset.zero), radius: 23);
    final paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..strokeCap = StrokeCap.round;

    canvas.drawCircle(rect.center, 23, paint..color = track);
    if (progress > 0) {
      canvas.drawArc(rect, -1.5707963267948966, 6.283185307179586 * progress, false, paint..color = arc);
    }
  }

  @override
  bool shouldRepaint(_RingPainter old) =>
      old.progress != progress || old.arc != arc || old.track != track;
}

/// Why a locked ticket can't be opened yet.
class _LockedNote extends StatelessWidget {
  const _LockedNote();

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(color: AppColours.surfaceMuted, borderRadius: BorderRadius.circular(14)),
      child: Row(
        children: [
          const Icon(Icons.lock_outline_rounded, size: 16, color: AppColours.inkMuted),
          const SizedBox(width: 8),
          Expanded(
            child: Text(t.machineAvailableAfterStart, style: AppTextStyles.caption.copyWith(height: 18 / 13)),
          ),
        ],
      ),
    );
  }
}
