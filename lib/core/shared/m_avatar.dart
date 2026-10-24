import 'package:flutter/material.dart';

/// Circular initial avatar, coloured from a small palette picked
/// deterministically from [seed] (typically the person's full name).
class MAvatar extends StatelessWidget {
  final String initial;
  final String seed;
  final double radius;

  const MAvatar({
    super.key,
    required this.initial,
    required this.seed,
    this.radius = 22,
  });

  static const _palette = [
    Color(0xFFFBD5CB),
    Color(0xFFF7E3B0),
    Color(0xFFE2E0D8),
    Color(0xFFCFE8DC),
  ];

  @override
  Widget build(BuildContext context) {
    final color = _palette[seed.hashCode.abs() % _palette.length];
    return CircleAvatar(
      radius: radius,
      backgroundColor: color,
      child: Text(
        initial.toUpperCase(),
        style: TextStyle(
          fontSize: radius * 0.73,
          fontWeight: FontWeight.w800,
          color: Colors.black87,
        ),
      ),
    );
  }
}
