import 'dart:convert';
import 'dart:ui' as ui;

import 'package:flutter/painting.dart';

/// The drawn signature as the API takes it: a transparent PNG of the ink,
/// base64-encoded in a `data:image/png;base64,…` URL.
///
/// [strokes] is what the signature pad produces — one flat, 0..1-normalised
/// `[x, y, ...]` list per stroke.
Future<String> signatureDataUrl(List<List<double>> strokes) async {
  const width = 600.0;
  const height = 290.0;

  final recorder = ui.PictureRecorder();
  final canvas = Canvas(recorder, const Rect.fromLTWH(0, 0, width, height));
  final ink = Paint()
    ..color = const Color(0xFF1E3A8A)
    ..style = PaintingStyle.stroke
    ..strokeWidth = 6
    ..strokeCap = StrokeCap.round
    ..strokeJoin = StrokeJoin.round;
  for (final stroke in strokes) {
    if (stroke.length < 4) continue;
    final path = Path()..moveTo(stroke[0] * width, stroke[1] * height);
    for (var i = 2; i + 1 < stroke.length; i += 2) {
      path.lineTo(stroke[i] * width, stroke[i + 1] * height);
    }
    canvas.drawPath(path, ink);
  }

  final image = await recorder.endRecording().toImage(width.toInt(), height.toInt());
  final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
  image.dispose();
  return 'data:image/png;base64,${base64Encode(bytes!.buffer.asUint8List())}';
}
