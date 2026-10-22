import 'package:flutter/material.dart';

/// Reusable rounded card container: full-width, a solid background colour,
/// and consistent padding/corner radius. Used for white content cards and
/// coloured panels alike (pass [color] to override the default white).
class MCard extends StatelessWidget {
  final Widget child;
  final Color color;
  final double borderRadius;
  final EdgeInsetsGeometry padding;

  const MCard({
    super.key,
    required this.child,
    this.color = Colors.white,
    this.borderRadius = 24,
    this.padding = const EdgeInsets.all(20),
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: padding,
      decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(borderRadius)),
      child: child,
    );
  }
}
