import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../machines/domain/entities/job_machines.dart';
import '../../../machines/domain/entities/machine_status.dart';
import '../../../machines/domain/usecases/get_machine_pdf.dart';
import '../../../machines/presentation/widgets/machine_widgets.dart';
import '../../../requests/domain/entities/inspection_request.dart';

// Values from the review-summary design that have no token of their own.
const _outline = Color(0xFFDDE2EE);
const _perforation = Color(0xFFD3D9E8);
const _timelineDash = Color(0xFFC3CBDE);
const _greenEdge = Color(0xFFBCE0CC);
const _greenDetail = Color(0xFF2A4A3A);
const _redEdge = Color(0xFFF1C4BF);
const _dueAmber = Color(0xFFC77700);

TextStyle _mono(double size, {Color color = AppColours.ink}) =>
    TextStyle(
      fontFamily: 'monospace',
      fontFamilyFallback: const ['Menlo', 'Courier New', 'Courier'],
      fontSize: size,
      fontWeight: FontWeight.w500,
      color: color,
    );

bool _filled(String? value) =>
    value != null && value
        .trim()
        .isNotEmpty;

/// One machine of the job under review, drawn as a ticket: what the machine is
/// and the certificate's verdict (tap it to preview the PDF) on top, then —
/// past the perforation — when it was examined and when it is next due, the
/// key data printed on the certificate and the standards it was done to.
class ReviewMachineTicket extends StatelessWidget {
  final InspectionRequest request;
  final MachineSummary machine;

  const ReviewMachineTicket({
    super.key,
    required this.request,
    required this.machine,
  });

  Future<void> _preview(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final number = machine.certificateNumber ?? certNumberFor(request);
    return PdfPreviewPage.open(
      context,
      title: t.certificateTitle,
      subtitle: number,
      fileName: '$number.pdf',
      build: () => getIt<GetMachinePdf>()(request.id, machine.requestItemId),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final facts = <(String, String)>[
      if (_filled(machine.capacity)) (t.capacityLabel, machine.capacity!),
      if (_filled(machine.manufacturer))
        (t.manufacturerLabel, machine.manufacturer!),
      if (_filled(machine.model)) (t.modelYearShortLabel, machine.model!),
    ];
    final hasDetails =
        _filled(machine.examinationDate) ||
            _filled(machine.nextExaminationDate) ||
            facts.isNotEmpty ||
            machine.inspectionStandards.isNotEmpty;

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(26),
        boxShadow: const [
          BoxShadow(
              color: Color(0x0F0B1A3B), blurRadius: 2, offset: Offset(0, 1)),
          BoxShadow(
            color: Color(0x290B1A3B),
            blurRadius: 34,
            spreadRadius: -10,
            offset: Offset(0, 14),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: EdgeInsets.fromLTRB(18, 18, 18, hasDetails ? 8 : 18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _TitleRow(machine: machine),
                const SizedBox(height: 12),
                _Tags(machine: machine),
                if (machine.reviewNote != null) ...[
                  const SizedBox(height: 12),
                  ReviewNote(note: machine.reviewNote!),
                ],
                const SizedBox(height: 12),
                _ResultTile(machine: machine, onTap: () => _preview(context)),
              ],
            ),
          ),
          if (hasDetails) ...[
            const _Perforation(),
            Padding(
              padding: const EdgeInsets.fromLTRB(18, 8, 18, 16),
              child: _Details(machine: machine, facts: facts),
            ),
          ],
        ],
      ),
    );
  }
}

/// The machine's icon, name, kind and quantity.
class _TitleRow extends StatelessWidget {
  final MachineSummary machine;

  const _TitleRow({required this.machine});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final kind = [
      machine.type,
      machine.templateName,
    ].whereType<String>().where((part) =>
    part
        .trim()
        .isNotEmpty).join(' · ');
    return Row(
      children: [
        Container(
          width: 52,
          height: 52,
          decoration: BoxDecoration(
            color: AppColours.navy,
            borderRadius: BorderRadius.circular(16),
          ),
          child: const Icon(
            Icons.precision_manufacturing_outlined,
            size: 28,
            color: Colors.white,
          ),
        ),
        const SizedBox(width: 14),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                machine.name,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: AppTextStyles.sectionTitle.copyWith(
                  fontSize: 22,
                  height: 1.15,
                  fontWeight: FontWeight.w700,
                  letterSpacing: -0.44,
                ),
              ),
              if (kind.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.only(top: 3),
                  child: Text(
                    kind,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: AppTextStyles.subtitle.copyWith(height: 1.3),
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(width: 10),
        Container(
          height: 32,
          padding: const EdgeInsets.symmetric(horizontal: 10),
          alignment: Alignment.center,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(10),
            border: Border.all(color: _outline),
          ),
          child: Text(
            t.machineQuantityLabel(machine.quantity),
            style: _mono(13, color: AppColours.inkMuted),
          ),
        ),
      ],
    );
  }
}

/// Where the certificate stands, the machine's serial number and — once
/// approved — the certificate number.
class _Tags extends StatelessWidget {
  final MachineSummary machine;

  const _Tags({required this.machine});

  static IconData? _iconOf(MachineStatus status) =>
      switch (status) {
        MachineStatus.submitted ||
        MachineStatus.approved ||
        MachineStatus.delivered => Icons.check_circle_outline_rounded,
        MachineStatus.returned => Icons.replay_rounded,
        MachineStatus.draft => Icons.edit_outlined,
        MachineStatus.notStarted => null,
      };

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        MachineStatusChip(
          status: machine.status,
          icon: _iconOf(machine.status),
        ),
        if (_filled(machine.serialNumber))
          Container(
            height: 28,
            padding: const EdgeInsetsDirectional.fromSTEB(9, 0, 11, 0),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(999),
              border: Border.all(color: _outline),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(
                  Icons.tag_rounded,
                  size: 14,
                  color: AppColours.inkMuted,
                ),
                const SizedBox(width: 5),
                Text(machine.serialNumber!, style: _mono(13)),
              ],
            ),
          ),
        if (machine.certificateNumber != null)
          Container(
            height: 28,
            padding: const EdgeInsets.symmetric(horizontal: 11),
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: AppColours.successBackground,
              borderRadius: BorderRadius.circular(999),
            ),
            child: Text(
              t.machineCertificateNumber(machine.certificateNumber!),
              style: AppTextStyles.badge.copyWith(
                color: AppColours.successText,
              ),
            ),
          ),
      ],
    );
  }
}

/// How a certificate's verdict is painted.
class _Verdict {
  final String label;
  final IconData icon;
  final Color background;
  final Color border;
  final Color accent;
  final Color detail;

  const _Verdict({
    required this.label,
    required this.icon,
    required this.background,
    required this.border,
    required this.accent,
    required this.detail,
  });
}

/// The certificate's verdict with the checklist ring; tapping it opens the
/// certificate PDF.
class _ResultTile extends StatelessWidget {
  final MachineSummary machine;
  final VoidCallback onTap;

  const _ResultTile({required this.machine, required this.onTap});

  /// Satisfactory in green, unsatisfactory in red; anything else (or nothing
  /// recorded) in grey.
  _Verdict _verdict(AppLocalizations t) {
    final result = machine.result;
    return switch (result?.toLowerCase()) {
      'satisfactory' =>
          _Verdict(
            label: t.resultSatisfactory,
            icon: Icons.check_rounded,
            background: AppColours.successBackground,
            border: _greenEdge,
            accent: AppColours.successText,
            detail: _greenDetail,
          ),
      'unsatisfactory' =>
          _Verdict(
            label: t.resultUnsatisfactory,
            icon: Icons.close_rounded,
            background: AppColours.errorBackground,
            border: _redEdge,
            accent: AppColours.errorText,
            detail: AppColours.inkBody,
          ),
      _ =>
          _Verdict(
            label: result ?? '—',
            icon: Icons.remove_rounded,
            background: AppColours.surfaceMuted,
            border: AppColours.borderSubtle,
            accent: AppColours.inkSecondary,
            detail: AppColours.inkSecondary,
          ),
    };
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final verdict = _verdict(t);
    final total = machine.checklistTotal;
    final fraction = total == 0
        ? 1.0
        : (machine.checklistAnswered / total).clamp(0.0, 1.0);

    return Container(
      decoration: BoxDecoration(
        color: verdict.background,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: verdict.border),
      ),
      clipBehavior: Clip.antiAlias,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(14),
            child: Row(
              children: [
                _ProgressRing(
                  fraction: fraction,
                  track: verdict.border,
                  color: verdict.accent,
                  icon: verdict.icon,
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        t.examinationResultLabel.toUpperCase(),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AppTextStyles.badge.copyWith(
                          color: verdict.accent,
                          letterSpacing: 0.48,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        verdict.label,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AppTextStyles.sectionTitle.copyWith(
                          fontSize: 22,
                          height: 1.15,
                          fontWeight: FontWeight.w700,
                          letterSpacing: -0.22,
                          color: verdict.accent,
                        ),
                      ),
                      if (total > 0) ...[
                        const SizedBox(height: 2),
                        Text(
                          t.machineChecklistProgress(
                            machine.checklistAnswered,
                            total,
                          ),
                          style: AppTextStyles.caption.copyWith(
                            height: 1.3,
                            color: verdict.detail,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Container(
                      width: 36,
                      height: 36,
                      decoration: const BoxDecoration(
                        color: Colors.white,
                        shape: BoxShape.circle,
                        boxShadow: [
                          BoxShadow(
                            color: Color(0x1F0B1A3B),
                            blurRadius: 3,
                            offset: Offset(0, 1),
                          ),
                        ],
                      ),
                      child: const Icon(
                        Icons.chevron_right_rounded,
                        size: 22,
                        color: AppColours.ink,
                      ),
                    ),
                    const SizedBox(height: 3),
                    Text(
                      t.previewAction,
                      style: AppTextStyles.badge.copyWith(
                        color: AppColours.ink,
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
  }
}

/// A ring that sweeps to [fraction] of a circle, with a glyph in its middle.
class _ProgressRing extends StatelessWidget {
  static const _size = 56.0;

  final double fraction;
  final Color track;
  final Color color;
  final IconData icon;

  const _ProgressRing({
    required this.fraction,
    required this.track,
    required this.color,
    required this.icon,
  });

  @override
  Widget build(BuildContext context) {
    return Skeleton.replace(
      width: _size,
      height: _size,
      replacement: const DecoratedBox(
        decoration: BoxDecoration(color: Colors.black, shape: BoxShape.circle),
      ),
      child: TweenAnimationBuilder<double>(
        tween: Tween(begin: 0, end: fraction),
        duration: MediaQuery.disableAnimationsOf(context)
            ? Duration.zero
            : const Duration(milliseconds: 1000),
        curve: Curves.easeInOutCubic,
        builder: (context, value, child) =>
            CustomPaint(
              size: const Size.square(_size),
              painter: _RingPainter(
                  progress: value, track: track, color: color),
              child: child,
            ),
        child: Center(child: Icon(icon, size: 24, color: color)),
      ),
    );
  }
}

class _RingPainter extends CustomPainter {
  static const _radius = 23.0;
  static const _stroke = 6.0;

  final double progress;
  final Color track;
  final Color color;

  const _RingPainter({
    required this.progress,
    required this.track,
    required this.color,
  });

  @override
  void paint(Canvas canvas, Size size) {
    final circle = Rect.fromCircle(
      center: size.center(Offset.zero),
      radius: _radius,
    );
    canvas.drawOval(
      circle,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = _stroke
        ..color = track,
    );
    if (progress <= 0) return;
    canvas.drawArc(
      circle,
      -1.5707963267948966,
      6.283185307179586 * progress,
      false,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = _stroke
        ..strokeCap = StrokeCap.round
        ..color = color,
    );
  }

  @override
  bool shouldRepaint(_RingPainter old) =>
      old.progress != progress || old.track != track || old.color != color;
}

/// The ticket's tear line: a dashed rule with a notch bitten out of each side.
class _Perforation extends StatelessWidget {
  const _Perforation();

  @override
  Widget build(BuildContext context) {
    Widget notch() =>
        Container(
          width: 20,
          height: 20,
          decoration: const BoxDecoration(
            color: AppColours.background,
            shape: BoxShape.circle,
          ),
        );
    return SizedBox(
      height: 20,
      child: Stack(
        clipBehavior: Clip.none,
        children: [
          Positioned(left: -10, top: 0, child: notch()),
          Positioned(right: -10, top: 0, child: notch()),
          const Positioned(
            left: 18,
            right: 18,
            top: 9,
            child: _DashedLine(color: _perforation),
          ),
        ],
      ),
    );
  }
}

/// A horizontal dashed rule that fills the width it is given.
class _DashedLine extends StatelessWidget {
  final Color color;

  const _DashedLine({required this.color});

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 2,
      child: CustomPaint(painter: _DashPainter(color)),
    );
  }
}

class _DashPainter extends CustomPainter {
  static const _dash = 6.0;
  static const _gap = 4.0;

  final Color color;

  const _DashPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..strokeWidth = size.height
      ..style = PaintingStyle.stroke;
    final y = size.height / 2;
    var x = 0.0;
    while (x < size.width) {
      final end = x + _dash;
      canvas.drawLine(
          Offset(x, y), Offset(end > size.width ? size.width : end, y), paint);
      x += _dash + _gap;
    }
  }

  @override
  bool shouldRepaint(_DashPainter old) => old.color != color;
}

/// Below the perforation: the examination dates on a small timeline, the
/// certificate's key data, and the standards.
class _Details extends StatelessWidget {
  final MachineSummary machine;
  final List<(String, String)> facts;

  const _Details({required this.machine, required this.facts});

  /// The interval between the two dates, in whole months.
  static int _months(DateTime from, DateTime to) =>
      (to
          .difference(from)
          .inDays / 30.4375).round();

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations
        .localeOf(context)
        .languageCode;
    final examination = _filled(machine.examinationDate)
        ? machine.examinationDate
        : null;
    final next = _filled(machine.nextExaminationDate)
        ? machine.nextExaminationDate
        : null;
    final examinedAt = examination == null
        ? null
        : DateTime.tryParse(examination)?.toLocal();
    final dueAt = next == null ? null : DateTime.tryParse(next)?.toLocal();
    String date(String raw, DateTime? parsed) =>
        parsed == null ? raw : DateFormat('d MMM yyyy', locale).format(parsed);
    final months = examinedAt != null && dueAt != null
        ? _months(examinedAt, dueAt)
        : 0;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (examination != null || next != null) ...[
          if (examination != null && next != null) ...[
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 4),
              child: Row(
                children: [
                  const _Dot(
                    fill: AppColours.successText,
                    halo: AppColours.successBackground,
                  ),
                  const SizedBox(width: 8),
                  const Expanded(child: _DashedLine(color: _timelineDash)),
                  if (months > 0) ...[
                    const SizedBox(width: 8),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 10,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: AppColours.surfaceMuted,
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text(
                        t.examinationIntervalMonths(months),
                        maxLines: 1,
                        style: AppTextStyles.badge.copyWith(
                          color: AppColours.inkSecondary,
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    const Expanded(child: _DashedLine(color: _timelineDash)),
                  ],
                  const SizedBox(width: 8),
                  const _Dot(
                    fill: Colors.white,
                    halo: AppColours.chipAmberBackground,
                    ring: _dueAmber,
                  ),
                ],
              ),
            ),
            const SizedBox(height: 6),
          ],
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (examination != null)
                Expanded(
                  child: _DateBlock(
                    label: t.examinationDateLabel,
                    value: date(examination, examinedAt),
                    labelColor: AppColours.inkMuted,
                    labelWeight: FontWeight.w500,
                  ),
                ),
              if (examination != null && next != null)
                const SizedBox(width: 12),
              if (next != null)
                Expanded(
                  child: _DateBlock(
                    label: t.nextExaminationDateLabel,
                    value: date(next, dueAt),
                    labelColor: AppColours.chipAmberText,
                    labelWeight: FontWeight.w600,
                    alignEnd: examination != null,
                  ),
                ),
            ],
          ),
          const SizedBox(height: 14),
        ],
        if (facts.isNotEmpty) ...[
          IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                for (var i = 0; i < facts.length; i++) ...[
                  if (i > 0) const SizedBox(width: 8),
                  Expanded(
                    child: _FactTile(label: facts[i].$1, value: facts[i].$2),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: 14),
        ],
        if (machine.inspectionStandards.isNotEmpty) ...[
          Text(
            t.machineHeaderStandards,
            style: AppTextStyles.caption.copyWith(
              fontSize: 12,
              height: 16 / 12,
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final standard in machine.inspectionStandards)
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 10,
                    vertical: 6,
                  ),
                  decoration: BoxDecoration(
                    color: AppColours.surfaceMuted,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(standard, style: _mono(12)),
                ),
            ],
          ),
        ],
      ],
    );
  }
}

/// One end of the timeline: a filled dot, or a ring when [ring] is set.
class _Dot extends StatelessWidget {
  final Color fill;
  final Color halo;
  final Color? ring;

  const _Dot({required this.fill, required this.halo, this.ring});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 14,
      height: 14,
      decoration: BoxDecoration(
        color: fill,
        shape: BoxShape.circle,
        border: ring == null ? null : Border.all(color: ring!, width: 3),
        boxShadow: [BoxShadow(color: halo, spreadRadius: 4)],
      ),
    );
  }
}

class _DateBlock extends StatelessWidget {
  final String label;
  final String value;
  final Color labelColor;
  final FontWeight labelWeight;
  final bool alignEnd;

  const _DateBlock({
    required this.label,
    required this.value,
    required this.labelColor,
    required this.labelWeight,
    this.alignEnd = false,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: alignEnd
          ? CrossAxisAlignment.end
          : CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          label,
          textAlign: alignEnd ? TextAlign.end : TextAlign.start,
          style: AppTextStyles.caption.copyWith(
            fontSize: 12,
            height: 16 / 12,
            fontWeight: labelWeight,
            color: labelColor,
          ),
        ),
        const SizedBox(height: 2),
        Text(
          value,
          textAlign: alignEnd ? TextAlign.end : TextAlign.start,
          style: AppTextStyles.sectionTitle.copyWith(letterSpacing: -0.18),
        ),
      ],
    );
  }
}

class _FactTile extends StatelessWidget {
  final String label;
  final String value;

  const _FactTile({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Container(
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
            label,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.caption.copyWith(
              fontSize: 12,
              height: 16 / 12,
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(height: 3),
          Text(
            value,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.emphasis,
          ),
        ],
      ),
    );
  }
}
