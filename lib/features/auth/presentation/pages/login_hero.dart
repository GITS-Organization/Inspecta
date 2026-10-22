import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/widgets/brand_mark.dart';
import '../../../../core/widgets/network_painter.dart';
import '../../../../l10n/app_localizations.dart';

const _accent = AppColours.successOnNavy;

/// The sign-in screen's animated backdrop: a deep-navy gradient with a faint
/// network drifting across it and two soft circles breathing out of step,
/// filling the whole screen. [child] sits on top of it.
class LoginHero extends StatelessWidget {
  const LoginHero({super.key, required this.child, this.animate = true});

  final Widget child;

  /// Tests switch the motion off: a repeating animation never settles.
  final bool animate;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFF14306B), AppColours.navy, Color(0xFF060D20)],
          stops: [0, 0.5, 1],
        ),
      ),
      child: Stack(
        fit: StackFit.expand,
        children: [
          _Drift(
            animate: animate,
            duration: const Duration(seconds: 26),
            curve: Curves.easeInOut,
            offset: const Offset(-18, 14),
            scaleFrom: 1.02,
            scaleTo: 1.06,
            child: const CustomPaint(painter: NetworkPainter(nodes: 22)),
          ),
          // Two soft circles that drift and breathe, out of step with each other.
          _AmbientCircle(
            animate: animate,
            size: 280,
            centre: const Alignment(0.9, -0.85),
            color: _accent.withAlpha(0x1F),
            duration: const Duration(seconds: 11),
            offset: const Offset(-40, 40),
            scaleTo: 1.2,
          ),
          _AmbientCircle(
            animate: animate,
            size: 200,
            centre: const Alignment(-0.9, 0.2),
            color: Colors.white.withAlpha(0x14),
            duration: const Duration(seconds: 14),
            offset: const Offset(35, -30),
            scaleTo: 0.82,
          ),
          child,
        ],
      ),
    );
  }
}

/// The brand and headline that sit on [LoginHero], above the form: logo,
/// eyebrow, the two-line headline and the one-line pitch.
class LoginIntro extends StatelessWidget {
  const LoginIntro({super.key});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final headline = AppTextStyles.pageTitle.copyWith(
      fontSize: 36,
      height: 1.1,
      letterSpacing: -0.9,
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            const BrandMark(size: 44),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Integrity',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Colors.white,
                    ),
                  ),
                  Text(
                    'INSPECTION & ENGINEERING',
                    style: TextStyle(
                      fontSize: 10.5,
                      letterSpacing: 1.4,
                      fontWeight: FontWeight.w600,
                      color: Colors.white.withAlpha(0x99),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: 32),
        Row(
          children: [
            Container(width: 28, height: 1.5, color: _accent),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                t.loginHeroEyebrow,
                style: const TextStyle(
                  fontSize: 12,
                  letterSpacing: 1.6,
                  fontWeight: FontWeight.w700,
                  color: _accent,
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 14),
        Text(t.loginHeroLine1, style: headline.copyWith(color: Colors.white)),
        Text(t.loginHeroLine2, style: headline.copyWith(color: _accent)),
      ],
    );
  }
}

/// Moves [child] from rest to [offset] and back, forever, scaling as it goes.
class _Drift extends StatefulWidget {
  const _Drift({
    required this.animate,
    required this.duration,
    required this.offset,
    required this.child,
    this.curve = const Cubic(0.2, 0.8, 0.2, 1),
    this.scaleFrom = 1,
    this.scaleTo = 1,
  });

  final bool animate;
  final Duration duration;
  final Curve curve;
  final Offset offset;
  final double scaleFrom, scaleTo;
  final Widget child;

  @override
  State<_Drift> createState() => _DriftState();
}

class _DriftState extends State<_Drift> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(
    vsync: this,
    duration: widget.duration,
  );
  late final Animation<double> _t = CurvedAnimation(
    parent: _c,
    curve: widget.curve,
  );

  @override
  void initState() {
    super.initState();
    if (widget.animate) _c.repeat(reverse: true);
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return RepaintBoundary(
      child: AnimatedBuilder(
        animation: _t,
        child: widget.child,
        builder: (context, child) => Transform.translate(
          offset: widget.offset * _t.value,
          child: Transform.scale(
            scale:
                widget.scaleFrom +
                (widget.scaleTo - widget.scaleFrom) * _t.value,
            child: child,
          ),
        ),
      ),
    );
  }
}

class _AmbientCircle extends StatelessWidget {
  const _AmbientCircle({
    required this.animate,
    required this.size,
    required this.centre,
    required this.color,
    required this.duration,
    required this.offset,
    required this.scaleTo,
  });

  final bool animate;
  final double size;

  /// Where the circle's centre sits in the panel, as an [Alignment].
  final Alignment centre;
  final Color color;
  final Duration duration;
  final Offset offset;
  final double scaleTo;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: centre,
      child: _Drift(
        animate: animate,
        duration: duration,
        offset: offset,
        scaleTo: scaleTo,
        child: Container(
          width: size,
          height: size,
          decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        ),
      ),
    );
  }
}
