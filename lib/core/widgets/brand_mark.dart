import 'package:flutter/material.dart';

/// The Integrity emblem on a white rounded tile — for sitting on a dark
/// panel (the sign-in hero).
class BrandMark extends StatelessWidget {
  const BrandMark({super.key, this.size = 44});

  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      padding: EdgeInsets.all(size * 0.1),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(size * 0.3),
      ),
      child: ClipRect(
        child: Image.asset(
          'assets/pngs/integraty-logo.png',
          fit: BoxFit.cover,
          // The artwork's emblem sits above its wordmark; keep the emblem.
          alignment: const Alignment(0, -0.75),
          semanticLabel: 'Integrity',
        ),
      ),
    );
  }
}
