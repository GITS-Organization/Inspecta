import 'package:flutter/material.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';

/// The pieces the inspector's redesigned screens (Tasks and Items) share: the
/// type, the card shadow, the entrance, the segmented progress bar, the pulse
/// dot and the big action button.

/// Arabic letters join, so tracking (letter-spacing) would pull them apart.
bool isArabic(BuildContext context) =>
    Localizations.localeOf(context).languageCode == 'ar';

/// Headings of the redesigned cards: heavy and tightly tracked.
TextStyle inspectorDisplay(
  BuildContext context,
  double size, {
  FontWeight weight = FontWeight.w700,
  double height = 1.1,
}) => AppTextStyles.sectionTitle.copyWith(
  fontSize: size,
  height: height,
  fontWeight: weight,
  letterSpacing: isArabic(context) ? 0 : -size * 0.02,
);

/// The small uppercase heading of a block ("CERTIFICATES").
TextStyle inspectorEyebrow(BuildContext context) => TextStyle(
  fontSize: 13,
  height: 1.3,
  fontWeight: FontWeight.w700,
  letterSpacing: isArabic(context) ? 0 : 0.52,
  color: AppColours.inkMuted,
);

/// Codes and serial numbers: quotation numbers, S/N, standards. The platform's
/// own monospace face, so nothing extra to bundle.
const TextStyle inspectorMono = TextStyle(
  fontFamily: 'monospace',
  fontFamilyFallback: ['Menlo', 'Roboto Mono', 'Courier New'],
  fontWeight: FontWeight.w500,
  color: AppColours.ink,
);

/// A white card resting on the page: a hairline of shadow and a soft, long one.
final List<BoxShadow> inspectorCardShadow = [
  BoxShadow(
    color: AppColours.navy.withValues(alpha: 0.06),
    offset: const Offset(0, 1),
    blurRadius: 2,
  ),
  BoxShadow(
    color: AppColours.navy.withValues(alpha: 0.16),
    offset: const Offset(0, 14),
    blurRadius: 34,
    spreadRadius: -10,
  ),
];

/// The empty part of a progress bar.
const Color inspectorTrack = Color(0xFFD8DEEC);

/// Fades a card in while it rises 12px, once, when it first appears. Holds
/// still when the user asked the system to reduce animation.
class RiseIn extends StatelessWidget {
  static const _duration = Duration(milliseconds: 550);

  final Duration delay;
  final Widget child;

  const RiseIn({super.key, required this.child, this.delay = Duration.zero});

  @override
  Widget build(BuildContext context) {
    if (MediaQuery.disableAnimationsOf(context)) return child;

    final total = _duration + delay;
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: total,
      curve: Interval(
        delay.inMilliseconds / total.inMilliseconds,
        1,
        curve: const Cubic(0.2, 0.7, 0.2, 1),
      ),
      builder: (context, value, child) => Opacity(
        opacity: value,
        child: Transform.translate(
          offset: Offset(0, 12 * (1 - value)),
          child: child,
        ),
      ),
      child: child,
    );
  }
}

/// A progress bar with one segment per item (a certificate), the done ones
/// green. Past [maxSegments] the segments would be slivers, so it falls back
/// to one bar of the done share.
class SegmentBar extends StatelessWidget {
  static const maxSegments = 16;
  static const double height = 8;

  final int total;
  final int done;

  const SegmentBar({super.key, required this.total, required this.done});

  @override
  Widget build(BuildContext context) => Skeleton.replace(
    replacement: const Bone(
      width: double.infinity,
      height: height,
      uniRadius: 4,
    ),
    child: _bar(),
  );

  Widget _bar() {
    if (total > maxSegments) {
      return ClipRRect(
        borderRadius: BorderRadius.circular(999),
        child: LinearProgressIndicator(
          value: (done / total).clamp(0.0, 1.0),
          minHeight: height,
          color: AppColours.successIcon,
          backgroundColor: inspectorTrack,
        ),
      );
    }
    return Row(
      children: [
        for (var i = 0; i < total; i++)
          Expanded(
            child: Container(
              height: height,
              margin: EdgeInsetsDirectional.only(end: i == total - 1 ? 0 : 4),
              decoration: BoxDecoration(
                color: i < done ? AppColours.successIcon : inspectorTrack,
                borderRadius: BorderRadius.circular(4),
              ),
            ),
          ),
      ],
    );
  }
}

/// A small dot with a ring that swells and fades around it, for a status that
/// is live ("In progress"). A still dot when the user asked for less motion.
class PulseDot extends StatefulWidget {
  final Color color;
  final bool pulsing;

  const PulseDot({super.key, required this.color, this.pulsing = true});

  @override
  State<PulseDot> createState() => _PulseDotState();
}

class _PulseDotState extends State<PulseDot>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1800),
  );

  void _sync() {
    if (widget.pulsing && !MediaQuery.disableAnimationsOf(context)) {
      if (!_controller.isAnimating) _controller.repeat();
    } else {
      _controller.stop();
      _controller.value = 0;
    }
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _sync();
  }

  @override
  void didUpdateWidget(PulseDot old) {
    super.didUpdateWidget(old);
    if (old.pulsing != widget.pulsing) _sync();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, _) {
        final t = Curves.easeOut.transform(_controller.value);
        return Container(
          width: 8,
          height: 8,
          decoration: BoxDecoration(
            color: widget.color,
            shape: BoxShape.circle,
            boxShadow: _controller.isAnimating
                ? [
                    BoxShadow(
                      color: widget.color.withValues(alpha: 0.45 * (1 - t)),
                      spreadRadius: 8 * t,
                    ),
                  ]
                : null,
          ),
        );
      },
    );
  }
}

/// The card's one big action: full width, 54 high, with a label and an arrow.
/// [primary] is the navy "do it" button; otherwise it is a quiet white one for
/// something that only looks (View, View PDF).
class InspectorActionButton extends StatelessWidget {
  final String label;
  final VoidCallback onPressed;
  final bool primary;
  final IconData icon;

  const InspectorActionButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.primary = true,
    this.icon = Icons.arrow_forward_rounded,
  });

  @override
  Widget build(BuildContext context) => Skeleton.replace(
    // While loading it is a plain rounded bar. Drawn as itself, the button's
    // drop shadow would turn into a blurred dark smudge.
    replacement: const Bone(width: double.infinity, height: 54, uniRadius: 18),
    child: _button(),
  );

  Widget _button() {
    final foreground = primary ? Colors.white : AppColours.ink;
    final radius = BorderRadius.circular(18);

    return DecoratedBox(
      decoration: BoxDecoration(
        borderRadius: radius,
        boxShadow: primary
            ? [
                BoxShadow(
                  color: AppColours.navy.withValues(alpha: 0.5),
                  offset: const Offset(0, 8),
                  blurRadius: 18,
                  spreadRadius: -6,
                ),
              ]
            : null,
      ),
      child: Material(
        color: primary ? AppColours.primaryColor : Colors.white,
        shape: RoundedRectangleBorder(
          borderRadius: radius,
          side: primary
              ? BorderSide.none
              : const BorderSide(color: AppColours.border),
        ),
        child: InkWell(
          borderRadius: radius,
          onTap: onPressed,
          child: SizedBox(
            height: 54,
            child: Center(
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Flexible(
                      child: Text(
                        label,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AppTextStyles.buttonLabel.copyWith(
                          fontSize: 16,
                          letterSpacing: 0,
                          color: foreground,
                        ),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Icon(icon, size: 18, color: foreground),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// A job's or machine's status as a pill: a dot, then the label. The dot
/// pulses while [pulsing] — for something live, like an inspection under way.
class StatusPill extends StatelessWidget {
  final String label;
  final Color background;
  final Color foreground;
  final Color dot;
  final bool pulsing;

  const StatusPill({
    super.key,
    required this.label,
    required this.background,
    required this.foreground,
    required this.dot,
    this.pulsing = false,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 30,
      padding: const EdgeInsetsDirectional.fromSTEB(11, 0, 12, 0),
      decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(999)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          PulseDot(color: dot, pulsing: pulsing),
          const SizedBox(width: 7),
          Flexible(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: AppTextStyles.badge.copyWith(fontSize: 13, height: 16 / 13, color: foreground),
            ),
          ),
        ],
      ),
    );
  }
}

/// A ticket's tear line: a dashed rule with a bite out of each edge, in the
/// page's colour so the card reads as two stubs. Sits between two sections of
/// a card that rests on [AppColours.background].
class TicketPerforation extends StatelessWidget {
  static const double _notch = 20;

  const TicketPerforation({super.key});

  @override
  Widget build(BuildContext context) {
    // `Skeleton.keep`: the notches are the page's colour, which a loading
    // shimmer would otherwise turn into grey discs.
    return Skeleton.keep(
      child: ExcludeSemantics(
        child: SizedBox(
          height: _notch,
          child: Stack(
            clipBehavior: Clip.none,
            children: [
              PositionedDirectional(start: -_notch / 2, top: 0, child: _notchDisc()),
              PositionedDirectional(end: -_notch / 2, top: 0, child: _notchDisc()),
              const PositionedDirectional(
                start: 18,
                end: 18,
                top: 9,
                height: 2,
                child: CustomPaint(painter: _DashPainter()),
              ),
            ],
          ),
        ),
      ),
    );
  }

  static Widget _notchDisc() => Container(
    width: _notch,
    height: _notch,
    decoration: const BoxDecoration(color: AppColours.background, shape: BoxShape.circle),
  );
}

class _DashPainter extends CustomPainter {
  const _DashPainter();

  static const _dash = 6.0;
  static const _gap = 4.0;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = const Color(0xFFD3D9E8)
      ..strokeWidth = size.height;
    for (var x = 0.0; x < size.width; x += _dash + _gap) {
      canvas.drawLine(
        Offset(x, size.height / 2),
        Offset(x + _dash > size.width ? size.width : x + _dash, size.height / 2),
        paint,
      );
    }
  }

  @override
  bool shouldRepaint(_DashPainter old) => false;
}
