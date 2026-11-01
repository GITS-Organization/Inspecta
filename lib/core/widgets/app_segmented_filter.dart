import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../consts/app_colors.dart';
import '../consts/app_text_styles.dart';

/// One option of an [AppSegmentedFilter]: what it selects, how it reads, the
/// live number beside it (hidden at 0) and whether that number is an alarm.
class AppSegment<T> {
  final T value;
  final String label;
  final int count;

  /// Red count while it's above 0: something here needs the user. Ignored on
  /// the selected segment, whose count is white on the fill.
  final bool alert;

  const AppSegment({required this.value, required this.label, this.count = 0, this.alert = false});
}

/// The app's one filter / tab switch: a white track with the active segment
/// filled in navy and, optionally, a live count on each segment.
///
/// It lays itself out for the room it's given:
/// - few segments that fit: equal widths across the row, nothing scrolls;
/// - more than fit: the segments keep their natural widths and scroll
///   sideways inside the same track, so the control never clips a label;
/// - [shrinkWrap]: hugs its content, for use beside another control such as
///   a search box.
///
/// Tapping any segment, the selected one included, reports its value —
/// callers that let the user clear a filter (tap it again) decide that
/// themselves. [selected] may be null: no segment filled.
class AppSegmentedFilter<T> extends StatelessWidget {
  /// The track's height — also the height of a search box, so the two line up
  /// when they share a row.
  static const double height = 48;

  static const double _trackPadding = 4;

  /// The 1px border either side of the track.
  static const double _trackBorder = 1;

  /// What's left of [height] for a segment. Every segment is given exactly
  /// this, in every layout, so equal-width and scrolling tracks (and every
  /// screen) read as the same control.
  static const double _segmentHeight = height - 2 * (_trackPadding + _trackBorder);

  static const double _segmentPadding = 12;
  static const double _countGap = 6;

  final List<AppSegment<T>> segments;
  final T? selected;
  final ValueChanged<T> onChanged;

  /// Hug the content instead of filling the row.
  final bool shrinkWrap;

  /// Space around the track; by default the page gutter on both sides.
  final EdgeInsetsGeometry margin;

  const AppSegmentedFilter({
    super.key,
    required this.segments,
    required this.selected,
    required this.onChanged,
    this.shrinkWrap = false,
    this.margin = const EdgeInsets.symmetric(horizontal: 16),
  });

  static TextStyle _labelStyle(Color color) =>
      AppTextStyles.caption.copyWith(fontSize: 13, height: 1, fontWeight: FontWeight.w600, color: color);

  /// How wide [segment] wants to be: its label as the text scale sets it, the
  /// count pill if there is one, and the side padding.
  double _naturalWidth(BuildContext context, AppSegment<T> segment) {
    final painter = TextPainter(
      text: TextSpan(text: segment.label, style: _labelStyle(AppColours.ink)),
      textDirection: Directionality.of(context),
      textScaler: MediaQuery.textScalerOf(context),
      maxLines: 1,
    )..layout();
    final label = painter.width;
    painter.dispose();
    final pill = segment.count > 0
        ? _countGap + math.max(20, 12 + '${segment.count}'.length * 7.0)
        : 0.0;
    return label + pill + _segmentPadding * 2;
  }

  Widget _segment(AppSegment<T> segment) => _Segment(
    label: segment.label,
    count: segment.count,
    alert: segment.alert,
    selected: selected != null && segment.value == selected,
    onTap: () => onChanged(segment.value),
  );

  @override
  Widget build(BuildContext context) {
    final Widget content;
    if (shrinkWrap) {
      content = Row(
        mainAxisSize: MainAxisSize.min,
        children: [for (final segment in segments) _segment(segment)],
      );
    } else {
      content = LayoutBuilder(
        builder: (context, constraints) {
          // The inside of the track: the container has already taken its
          // padding and border off the width.
          final room = constraints.maxWidth;
          final widest = segments.map((s) => _naturalWidth(context, s)).fold(0.0, math.max);
          if (widest * segments.length <= room) {
            return Row(
              children: [
                for (final segment in segments) Expanded(child: _segment(segment)),
              ],
            );
          }
          return SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [for (final segment in segments) _segment(segment)],
            ),
          );
        },
      );
    }

    return Padding(
      padding: margin,
      child: Container(
        height: height,
        padding: const EdgeInsets.all(_trackPadding),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColours.border),
        ),
        child: content,
      ),
    );
  }
}

class _Segment extends StatelessWidget {
  final String label;
  final int count;
  final bool alert;
  final bool selected;
  final VoidCallback onTap;

  const _Segment({
    required this.label,
    required this.count,
    required this.alert,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final foreground = selected ? Colors.white : AppColours.ink;
    final Color countBackground;
    final Color countForeground;
    if (selected) {
      countBackground = Colors.white.withValues(alpha: 0.2);
      countForeground = Colors.white;
    } else if (alert) {
      countBackground = AppColours.errorIcon;
      countForeground = Colors.white;
    } else {
      countBackground = AppColours.primaryTint;
      countForeground = AppColours.primaryDark;
    }

    return Semantics(
      button: true,
      selected: selected,
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: AnimatedContainer(
          // Colour change only; nothing moves, and none at all when the user
          // asked the system to reduce animation.
          duration: MediaQuery.disableAnimationsOf(context) ? Duration.zero : const Duration(milliseconds: 180),
          curve: Curves.easeOutCubic,
          height: AppSegmentedFilter._segmentHeight,
          padding: const EdgeInsets.symmetric(horizontal: AppSegmentedFilter._segmentPadding),
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: selected ? AppColours.primaryColor : Colors.transparent,
            borderRadius: BorderRadius.circular(12),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Flexible(
                child: Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppSegmentedFilter._labelStyle(foreground),
                ),
              ),
              if (count > 0) ...[
                const SizedBox(width: AppSegmentedFilter._countGap),
                Container(
                  height: 20,
                  constraints: const BoxConstraints(minWidth: 20),
                  padding: const EdgeInsets.symmetric(horizontal: 6),
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: countBackground,
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    '$count',
                    // A fixed line height keeps the pill round instead of the
                    // font's leading stretching it into an oval.
                    style: AppTextStyles.badge.copyWith(
                      fontSize: 11,
                      height: 1,
                      fontWeight: FontWeight.w700,
                      color: countForeground,
                    ),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
