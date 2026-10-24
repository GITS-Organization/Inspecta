import 'dart:math' as math;

import 'package:flutter/material.dart';

class NetworkPainter extends CustomPainter {
  const NetworkPainter({
    this.nodes = 16,
    this.color = Colors.white,
    this.strength = 1,
    this.animation,
  }) : super(repaint: animation);

  /// How many dots to scatter; a tall, narrow panel wants fewer.
  final int nodes;

  /// Ink for the dots, links and rings: white on a dark panel, the brand
  /// blue on a light one.
  final Color color;

  /// Multiplies the base opacities; a light panel needs more than a dark one.
  final double strength;

  /// Loop position, 0 → 1. Null paints a still frame.
  final Animation<double>? animation;

  Color _ink(double alpha) =>
      color.withAlpha((alpha * strength).round().clamp(0, 0xFF));

  @override
  void paint(Canvas canvas, Size size) {
    final t = animation?.value ?? 0;
    const tau = 2 * math.pi;

    final ring = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1
      ..color = _ink(0x12);
    final centre = Offset(size.width * 0.9, size.height * 1.02);
    final breathe = 1 + 0.03 * math.sin(tau * t);
    for (final r in [0.28, 0.46, 0.66]) {
      canvas.drawCircle(centre, size.shortestSide * r * 1.3 * breathe, ring);
    }

    final rnd = math.Random(7);
    final drift = size.shortestSide * 0.05;
    final points = <Offset>[];
    for (var i = 0; i < nodes; i++) {
      final base = Offset(
        rnd.nextDouble() * size.width,
        rnd.nextDouble() * size.height,
      );
      // Whole-number orbit counts (1 or 2) keep the loop seamless; the
      // random phase and direction stop the nodes moving in lockstep.
      final phase = rnd.nextDouble() * tau;
      final turns = 1 + rnd.nextInt(2);
      final dir = rnd.nextBool() ? 1 : -1;
      final a = phase + dir * tau * turns * t;
      points.add(base + Offset(math.cos(a), math.sin(a)) * drift);
    }

    final link = Paint()..strokeWidth = 1;
    final reach = size.shortestSide * 0.45;
    for (var i = 0; i < points.length; i++) {
      for (var j = i + 1; j < points.length; j++) {
        final d = (points[i] - points[j]).distance;
        if (d < reach) {
          // Fade with length so links ease in and out as nodes move.
          link.color = _ink(0x14 * 1.6 * (1 - d / reach));
          canvas.drawLine(points[i], points[j], link);
        }
      }
    }

    final dot = Paint()..color = _ink(0x33);
    for (final n in points) {
      canvas.drawCircle(n, 2.5, dot);
    }
  }

  @override
  bool shouldRepaint(NetworkPainter old) =>
      old.nodes != nodes ||
      old.color != color ||
      old.strength != strength ||
      old.animation != animation;
}

/// A [NetworkPainter] that drives itself: one slow, looping cycle of
/// [duration]. Holds still when the platform asks to reduce motion.
class AnimatedNetwork extends StatefulWidget {
  const AnimatedNetwork({
    super.key,
    this.nodes = 16,
    this.color = Colors.white,
    this.strength = 1,
    this.duration = const Duration(seconds: 24),
  });

  final int nodes;
  final Color color;
  final double strength;
  final Duration duration;

  @override
  State<AnimatedNetwork> createState() => _AnimatedNetworkState();
}

class _AnimatedNetworkState extends State<AnimatedNetwork>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: widget.duration,
  );

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (MediaQuery.disableAnimationsOf(context)) {
      _controller.stop();
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
      child: CustomPaint(
        painter: NetworkPainter(
          nodes: widget.nodes,
          color: widget.color,
          strength: widget.strength,
          animation: _controller,
        ),
      ),
    );
  }
}
