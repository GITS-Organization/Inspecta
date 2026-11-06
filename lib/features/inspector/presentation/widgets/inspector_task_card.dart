import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import 'inspector_design.dart';

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
    final quotation = task.subject?.trim() ?? '';
    final title = task.senderLabel.isNotEmpty
        ? task.senderLabel
        : task.equipmentTitle;
    final counts = task.certificates;
    final units = task.totalUnits;

    return RiseIn(
      delay: const Duration(milliseconds: 50),
      child: Container(
        padding: const EdgeInsets.all(18),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(26),
          boxShadow: inspectorCardShadow,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              children: [
                Expanded(
                  child: Align(
                    alignment: AlignmentDirectional.centerStart,
                    child: quotation.isEmpty
                        ? const SizedBox.shrink()
                        : Skeleton.leaf(child: _QuotationChip(quotation)),
                  ),
                ),
                const SizedBox(width: 10),
                Skeleton.leaf(child: _StatusPill(status: task.status)),
              ],
            ),
            const SizedBox(height: 14),
            Text(
              title,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: inspectorDisplay(context, 20,),
            ),
            const SizedBox(height: 10),
            Wrap(
              spacing: 18,
              runSpacing: 8,
              children: [
                if (scheduledAt != null)
                  _Meta(
                    icon: Icons.schedule_rounded,
                    label: DateFormat('HH:mm', locale).format(scheduledAt),
                  ),
                if (task.location.isNotEmpty)
                  _Meta(icon: Icons.location_on_outlined, label: task.location),
                if (units > 0)
                  _Meta(
                    icon: Icons.inventory_2_outlined,
                    label: t.unitsCount(units),
                  ),
              ],
            ),
            const SizedBox(height: 16),
            _MapPreview(label: t.directionsAction, onTap: onNavigate),
            if (counts != null && counts.total > 0) ...[
              const SizedBox(height: 16),
              _CertificateProgress(counts: counts),
            ],
            const SizedBox(height: 16),
            InspectorActionButton(
              label: _notStarted
                  ? t.startInspectionAction
                  : t.continueCertificateAction,
              onPressed: onOpen,
            ),
          ],
        ),
      ),
    );
  }
}

/// The quotation number, in a quiet outlined chip.
class _QuotationChip extends StatelessWidget {
  final String label;

  const _QuotationChip(this.label);

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 30,
      padding: const EdgeInsets.symmetric(horizontal: 11),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: AppColours.borderSubtle),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.tag_rounded, size: 14, color: AppColours.inkMuted),
          const SizedBox(width: 6),
          Flexible(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: inspectorMono.copyWith(fontSize: 12),
            ),
          ),
        ],
      ),
    );
  }
}

/// Where the job stands, as a pill with a dot — which pulses while the
/// inspection is under way.
class _StatusPill extends StatelessWidget {
  final JobStatus status;

  const _StatusPill({required this.status});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final (background, textColor, dot, label) = switch (status) {
      JobStatus.assigned => (
        AppColours.chipAmberBackground,
        AppColours.chipAmberText,
        const Color(0xFFE0A21B),
        t.statusAssigned,
      ),
      JobStatus.taskAccepted => (
        AppColours.chipGreenBackground,
        AppColours.chipGreenText,
        AppColours.successIcon,
        t.taskAcceptedBadge,
      ),
      _ => (
        AppColours.chipBlueBackground,
        AppColours.chipBlueText,
        const Color(0xFF2F5BFF),
        t.taskInProgressBadge,
      ),
    };

    return Container(
      height: 30,
      padding: const EdgeInsetsDirectional.fromSTEB(11, 0, 12, 0),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          PulseDot(color: dot, pulsing: status == JobStatus.inProgress),
          const SizedBox(width: 7),
          Text(
            label,
            style: AppTextStyles.badge.copyWith(
              fontSize: 13,
              height: 16 / 13,
              color: textColor,
            ),
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
        Icon(icon, size: 16, color: AppColours.inkMuted),
        const SizedBox(width: 7),
        Flexible(
          child: Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.subtitle.copyWith(
              fontWeight: FontWeight.w500,
              color: AppColours.inkBody,
            ),
          ),
        ),
      ],
    );
  }
}

/// "CERTIFICATES · 0 of 1 submitted" over a segment per certificate.
class _CertificateProgress extends StatelessWidget {
  final CertificateCounts counts;

  const _CertificateProgress({required this.counts});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final label = t.certificatesLabel;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.baseline,
          textBaseline: TextBaseline.alphabetic,
          children: [
            Text(
              isArabic(context) ? label : label.toUpperCase(),
              style: inspectorEyebrow(context),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                t.machinesProgress(counts.submitted, counts.total),
                style: AppTextStyles.subtitle.copyWith(
                  fontWeight: FontWeight.w500,
                  color: AppColours.inkBody,
                ),
                textAlign: TextAlign.end,
                overflow: TextOverflow.ellipsis,
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        SegmentBar(total: counts.total, done: counts.submitted),
      ],
    );
  }
}

/// A drawn map of the site — roads, a park and a pin with a halo — with
/// Directions floating on it. It is a picture, not a live map: the button
/// hands the address to the phone's maps app.
class _MapPreview extends StatefulWidget {
  final String label;
  final VoidCallback onTap;

  const _MapPreview({required this.label, required this.onTap});

  @override
  State<_MapPreview> createState() => _MapPreviewState();
}

class _MapPreviewState extends State<_MapPreview>
    with SingleTickerProviderStateMixin {
  late final AnimationController _halo = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 2200),
  );

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (MediaQuery.disableAnimationsOf(context)) {
      _halo.stop();
    } else if (!_halo.isAnimating) {
      _halo.repeat();
    }
  }

  @override
  void dispose() {
    _halo.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Skeleton.replace(
      replacement: const Bone(
        width: double.infinity,
        height: 112,
        uniRadius: 18,
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(18),
        child: SizedBox(
          height: 112,
          child: Stack(
            fit: StackFit.expand,
            children: [
              ExcludeSemantics(
                child: RepaintBoundary(
                  child: CustomPaint(painter: _MapPainter(_halo)),
                ),
              ),
              PositionedDirectional(
                end: 10,
                bottom: 10,
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(999),
                    boxShadow: [
                      BoxShadow(
                        color: AppColours.navy.withValues(alpha: 0.25),
                        offset: const Offset(0, 4),
                        blurRadius: 14,
                        spreadRadius: -2,
                      ),
                    ],
                  ),
                  child: Material(
                    color: Colors.white,
                    shape: const StadiumBorder(),
                    child: InkWell(
                      customBorder: const StadiumBorder(),
                      onTap: widget.onTap,
                      child: Container(
                        height: 44,
                        padding: const EdgeInsetsDirectional.fromSTEB(
                          14,
                          0,
                          16,
                          0,
                        ),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(
                              Icons.near_me_rounded,
                              size: 17,
                              color: AppColours.ink,
                            ),
                            const SizedBox(width: 8),
                            Text(
                              widget.label,
                              style: AppTextStyles.buttonLabel.copyWith(
                                fontSize: 14,
                                letterSpacing: 0,
                                color: AppColours.ink,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The map picture, drawn on the design's 322 × 112 grid and stretched across
/// the width it is given (the height is fixed, so the pin never squashes).
class _MapPainter extends CustomPainter {
  final Animation<double> halo;

  _MapPainter(this.halo) : super(repaint: halo);

  static const _water = Color(0xFFDCE4F2);
  static const _park = Color(0xFFCBE5D4);
  static const _minorRoad = Color(0xFFEDF1F9);

  @override
  void paint(Canvas canvas, Size size) {
    final sx = size.width / 322;
    Offset at(double x, double y) => Offset(x * sx, y);

    canvas.drawRect(Offset.zero & size, Paint()..color = _water);
    canvas.drawRRect(
      RRect.fromLTRBR(162 * sx, -4, 240 * sx, 46, const Radius.circular(8)),
      Paint()..color = _park,
    );

    void road(List<Offset> points, double width, Color color) {
      final path = Path()..moveTo(points.first.dx, points.first.dy);
      for (final p in points.skip(1)) {
        path.lineTo(p.dx, p.dy);
      }
      canvas.drawPath(
        path,
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = width
          ..strokeCap = StrokeCap.round
          ..strokeJoin = StrokeJoin.round
          ..color = color,
      );
    }

    road(
      [at(-10, 98), at(110, 72), at(190, 86), at(340, 44)],
      12,
      Colors.white,
    );
    road([at(58, -10), at(84, 44), at(128, 130)], 8, Colors.white);
    road([at(252, -10), at(226, 52), at(276, 130)], 8, Colors.white);
    road([at(-10, 28), at(84, 40), at(226, 24), at(340, 28)], 6, _minorRoad);

    final pin = at(120, 56);
    final e = Curves.easeOut.transform(halo.value);
    canvas.drawCircle(
      pin,
      24 * (0.7 + 0.9 * e),
      Paint()..color = AppColours.navy.withValues(alpha: 0.22 * 0.5 * (1 - e)),
    );
    canvas.drawCircle(pin, 11, Paint()..color = AppColours.navy);
    canvas.drawCircle(pin, 4, Paint()..color = Colors.white);
  }

  @override
  bool shouldRepaint(_MapPainter old) => old.halo != halo;
}
