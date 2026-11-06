import 'dart:math' as math;

import 'package:flutter/material.dart';

/// How a screen's [AppBanner] is dressed.
enum AppBannerStyle {
  /// The app's default: pale gradient with the drifting network artwork.
  network,

  /// The inspector redesign: concentric rings that breathe and a few linked,
  /// drifting dots in the bottom-right corner, over a left-to-right gradient.
  /// Sized for a banner holding the greeting row, a title and a filter.
  rings,

  /// [rings] for a slim banner: a back button and a two-line title.
  ringsCompact;

  bool get isRings => this != network;
}

/// The background of an [AppBannerStyle.rings] banner.
const LinearGradient ringsBannerGradient = LinearGradient(
  // ~100deg: left to right, tipping slightly downward.
  begin: Alignment(-0.985, -0.174),
  end: Alignment(0.985, 0.174),
  stops: [0, 0.55, 1],
  colors: [Color(0xFFE3EAFA), Color(0xFFEEF2FC), Color(0xFFF4F7FD)],
);

/// The artwork of [AppBannerStyle.rings] / [AppBannerStyle.ringsCompact],
/// moving: it drives its own slow loop and holds still when the platform asks
/// to reduce motion.
class AnimatedBannerArt extends StatefulWidget {
  /// One loop. The rings and the dots each complete whole cycles in it, so the
  /// seam between loops is invisible.
  static const loop = Duration(seconds: 24);

  final bool compact;

  const AnimatedBannerArt({super.key, required this.compact});

  @override
  State<AnimatedBannerArt> createState() => _AnimatedBannerArtState();
}

class _AnimatedBannerArtState extends State<AnimatedBannerArt> with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(vsync: this, duration: AnimatedBannerArt.loop);

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (MediaQuery.disableAnimationsOf(context)) {
      _controller.stop();
      _controller.value = 0;
    } else if (!_controller.isAnimating) {
      _controller.repeat();
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return RepaintBoundary(
      child: CustomPaint(painter: BannerArtPainter(compact: widget.compact, animation: _controller)),
    );
  }
}

/// Paints the artwork of [AppBannerStyle.rings] / [AppBannerStyle.ringsCompact]:
/// concentric rings in the bottom-right corner and a few linked dots.
///
/// With an [animation] (0 → 1 over one loop) the rings breathe in a ripple, a
/// pulse spreads out from the corner now and then, and each dot drifts on a
/// small orbit with the link lines following. Without one it paints the still
/// frame.
///
/// Everything is anchored to the right edge (and the rings to the bottom), so
/// the picture sits in the same corner whatever the banner's width or height.
class BannerArtPainter extends CustomPainter {
  final bool compact;
  final Animation<double>? animation;

  const BannerArtPainter({required this.compact, this.animation}) : super(repaint: animation);

  static const _ringColors = [Color(0xFFC9D3EA), Color(0xFFD0D9EE), Color(0xFFD6DEF0), Color(0xFFDCE3F2)];
  static const _link = Color(0xFFC9D3EA);
  static const _node = Color(0xFFBAC5DE);

  static const _tau = 2 * math.pi;

  /// How far a dot strays from its place, in logical pixels.
  static const _drift = 4.0;

  /// Where each dot is in its orbit at the start, in radians, how many times it
  /// goes round per loop, and which way. Different per dot so they never move
  /// in lockstep.
  static const _phases = [0.0, 1.9, 3.4, 5.0];
  static const _turns = [1, 2, 1, 2];
  static const _directions = [1.0, -1.0, 1.0, -1.0];

  /// Whole breaths of the rings, and pulses from the corner, per loop.
  static const _breaths = 6;
  static const _pulses = 4;

  @override
  void paint(Canvas canvas, Size size) {
    final t = animation?.value ?? 0;
    final w = size.width;
    final h = size.height;

    final Offset centre;
    final List<double> radii;
    final List<Offset> dots;

    /// Which dots the link line runs through, in order.
    final List<int> line;
    if (compact) {
      centre = Offset(w - 13, h + 17);
      radii = const [28, 46, 64, 82];
      dots = [Offset(w - 74, 22), Offset(w - 46, 52), Offset(w - 16, 58), Offset(w - 28, 9)];
      line = const [0, 1, 2];
    } else {
      centre = Offset(w - 23, h - 7);
      radii = const [70, 108, 146];
      dots = [Offset(w - 122, 10), Offset(w - 107, 78), Offset(w - 66, 64), Offset(w - 153, 96)];
      line = const [1, 2];
    }

    final ring = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1;

    // The rings breathe one after another, so the swell travels outward.
    for (final (i, r) in radii.indexed) {
      final breath = 1 + 0.03 * math.sin(_tau * _breaths * t - i * 0.7);
      canvas.drawCircle(centre, r * breath, ring..color = _ringColors[i]);
    }

    // A pulse leaves the corner and fades as it spreads past the last ring.
    if (animation != null) {
      final p = (t * _pulses) % 1.0;
      final e = Curves.easeOut.transform(p);
      final from = radii.first * 0.4;
      final to = radii.last * 1.2;
      canvas.drawCircle(
        centre,
        from + (to - from) * e,
        ring..color = _ringColors.first.withValues(alpha: 0.7 * (1 - e)),
      );
    }

    // Each dot orbits its place.
    final moved = [
      for (final (i, home) in dots.indexed)
        home + Offset.fromDirection(_phases[i] + _directions[i] * _tau * _turns[i] * t, _drift),
    ];

    final path = Path()..moveTo(moved[line.first].dx, moved[line.first].dy);
    for (final i in line.skip(1)) {
      path.lineTo(moved[i].dx, moved[i].dy);
    }
    canvas.drawPath(
      path,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1
        ..color = _link,
    );

    final dotPaint = Paint()..color = _node;
    for (final p in moved) {
      canvas.drawCircle(p, 3.5, dotPaint);
    }
  }

  @override
  bool shouldRepaint(BannerArtPainter old) => old.compact != compact || old.animation != animation;
}
