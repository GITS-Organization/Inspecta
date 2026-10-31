import 'package:flutter/material.dart';

import '../consts/app_colors.dart';

/// A dashed box the reviewer signs in with a finger or stylus. Controlled:
/// [strokes] is what's drawn (one flat, 0..1-normalised `[x, y, ...]` list
/// per stroke) and [onChanged] gets the new list each time a stroke ends,
/// so clearing is just the parent passing an empty list.
class SignaturePad extends StatefulWidget {
  final List<List<double>> strokes;
  final ValueChanged<List<List<double>>> onChanged;
  final bool enabled;

  const SignaturePad({super.key, required this.strokes, required this.onChanged, this.enabled = true});

  @override
  State<SignaturePad> createState() => _SignaturePadState();
}

class _SignaturePadState extends State<SignaturePad> {
  List<double>? _current;

  Offset _normalise(Offset local, Size size) => Offset(
    (local.dx / size.width).clamp(0.0, 1.0),
    (local.dy / size.height).clamp(0.0, 1.0),
  );

  void _start(Offset local, Size size) {
    final p = _normalise(local, size);
    setState(() => _current = [p.dx, p.dy]);
  }

  void _extend(Offset local, Size size) {
    final current = _current;
    if (current == null) return;
    final p = _normalise(local, size);
    setState(() => current.addAll([p.dx, p.dy]));
  }

  void _end() {
    final current = _current;
    if (current == null) return;
    setState(() => _current = null);
    // A tap with no movement is a dot, not a signature stroke.
    if (current.length >= 4) widget.onChanged([...widget.strokes, current]);
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final size = Size(constraints.maxWidth, constraints.maxHeight);
        return GestureDetector(
          behavior: HitTestBehavior.opaque,
          onPanStart: widget.enabled ? (d) => _start(d.localPosition, size) : null,
          onPanUpdate: widget.enabled ? (d) => _extend(d.localPosition, size) : null,
          onPanEnd: widget.enabled ? (_) => _end() : null,
          onPanCancel: widget.enabled ? _end : null,
          child: CustomPaint(
            painter: _SignaturePainter(strokes: [...widget.strokes, ?_current]),
            size: size,
          ),
        );
      },
    );
  }
}

class _SignaturePainter extends CustomPainter {
  final List<List<double>> strokes;

  _SignaturePainter({required this.strokes});

  static const _dash = 6.0;
  static const _gap = 5.0;
  static const _radius = 18.0;

  @override
  void paint(Canvas canvas, Size size) {
    final box = RRect.fromRectAndRadius(Offset.zero & size, const Radius.circular(_radius));
    canvas.drawRRect(box, Paint()..color = AppColours.primarySoft);
    _drawDashedBorder(canvas, box);

    // Baseline the reviewer signs on.
    final baseline = Paint()
      ..color = AppColours.primaryTint
      ..strokeWidth = 3
      ..strokeCap = StrokeCap.round;
    canvas.drawLine(
      Offset(size.width * 0.2, size.height * 0.78),
      Offset(size.width * 0.8, size.height * 0.78),
      baseline,
    );

    _paintInk(canvas, size, strokes);
  }

  void _drawDashedBorder(Canvas canvas, RRect box) {
    final paint = Paint()
      ..color = AppColours.primaryColor.withValues(alpha: 0.45)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.5;
    for (final metric in (Path()..addRRect(box.deflate(0.75))).computeMetrics()) {
      var distance = 0.0;
      while (distance < metric.length) {
        canvas.drawPath(metric.extractPath(distance, distance + _dash), paint);
        distance += _dash + _gap;
      }
    }
  }

  @override
  bool shouldRepaint(_SignaturePainter oldDelegate) => true;
}

void _paintInk(Canvas canvas, Size size, List<List<double>> strokes) {
  final ink = Paint()
    ..color = const Color(0xFF1E3A8A)
    ..style = PaintingStyle.stroke
    ..strokeWidth = 3
    ..strokeCap = StrokeCap.round
    ..strokeJoin = StrokeJoin.round;
  for (final stroke in strokes) {
    if (stroke.length < 4) continue;
    final path = Path()..moveTo(stroke[0] * size.width, stroke[1] * size.height);
    for (var i = 2; i + 1 < stroke.length; i += 2) {
      path.lineTo(stroke[i] * size.width, stroke[i + 1] * size.height);
    }
    canvas.drawPath(path, ink);
  }
}
