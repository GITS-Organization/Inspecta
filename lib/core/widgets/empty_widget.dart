import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';

import '../consts/app_colors.dart';

/// Why a widget has nothing to show: each kind has its own glyph, accent and
/// motion, so a reader can tell "nothing yet" from "nothing matches" from "it
/// broke" before reading a word.
enum EmptyKind {
  /// Nothing has been recorded yet.
  nodata(Icons.inbox_rounded, Icons.refresh_rounded),

  /// A search found nothing.
  search(Icons.search_rounded, Icons.close_rounded),

  /// The filters leave nothing.
  filter(Icons.filter_alt_rounded, Icons.filter_alt_off_rounded),

  /// The data could not be loaded.
  error(Icons.cloud_off_rounded, Icons.refresh_rounded),

  /// Everything is handled: nothing is waiting.
  done(Icons.task_alt_rounded, Icons.history_rounded),

  /// The user may not see this section.
  locked(Icons.lock_rounded, Icons.send_rounded);

  /// The glyph on the tile, and the one on the action button.
  final IconData icon;
  final IconData actionIcon;

  const EmptyKind(this.icon, this.actionIcon);
}

/// What a list or panel shows when it has nothing to show: an animated
/// illustration of a stack of cards, a short [title], an optional [message]
/// and, when there is a next step, one action button. Motion stops under
/// reduced motion. Texts are passed in already localized.
///
/// Pick the [kind] that explains why it is empty, keep the title short and
/// offer one next step when there is one. A [compact] state is for small
/// panels: a smaller illustration and no minimum height.
class EmptyState extends StatefulWidget {
  final EmptyKind kind;
  final String title;
  final String? message;

  /// Replaces the glyph of the [kind].
  final IconData? icon;

  /// The action button shows when both are given.
  final String? actionLabel;
  final VoidCallback? onAction;
  final bool compact;

  const EmptyState({
    super.key,
    required this.title,
    this.kind = EmptyKind.nodata,
    this.message,
    this.icon,
    this.actionLabel,
    this.onAction,
    this.compact = false,
  });

  @override
  State<EmptyState> createState() => _EmptyStateState();
}

class _EmptyStateState extends State<EmptyState> with TickerProviderStateMixin {
  /// Seconds since the state appeared: every loop of the illustration is a
  /// function of it, so loops of different lengths need no controller each.
  final ValueNotifier<double> _clock = ValueNotifier(0);
  late final Ticker _ticker = createTicker(
    (elapsed) => _clock.value = elapsed.inMicroseconds / 1e6,
  );

  /// The title, message and button rise in one after the other.
  late final AnimationController _enter = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 660),
  );

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (MediaQuery.disableAnimationsOf(context)) {
      _ticker.stop();
      _clock.value = 0;
      _enter.value = 1;
    } else {
      if (!_ticker.isActive) _ticker.start();
      if (!_enter.isAnimating && _enter.value == 0) _enter.forward();
    }
  }

  @override
  void dispose() {
    _ticker.dispose();
    _enter.dispose();
    _clock.dispose();
    super.dispose();
  }

  Widget _rise(double delay, Widget child) {
    const total = 0.66;
    final window = Interval(
      delay / total,
      (delay + 0.5) / total,
      curve: Curves.easeOutCubic,
    );

    return AnimatedBuilder(
      animation: _enter,
      child: child,
      builder: (context, child) {
        final progress = window.transform(_enter.value);

        return Opacity(
          opacity: progress,
          child: Transform.translate(
            offset: Offset(0, 8 * (1 - progress)),
            child: child,
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final kind = widget.kind;
    final look = _Look.of(kind);
    final compact = widget.compact;
    final message = widget.message;
    final actionLabel = widget.actionLabel;
    final hasAction = widget.onAction != null && actionLabel != null;

    return Semantics(
      container: true,
      liveRegion: true,
      child: Center(
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: compact ? 0 : 220),
          child: FittedBox(
            fit: BoxFit.scaleDown,
            child: Padding(
              padding: compact
                  ? const EdgeInsets.all(10)
                  : const EdgeInsets.symmetric(vertical: 24, horizontal: 20),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                spacing: compact ? 6 : 12,
                children: [
                  SizedBox(
                    width: 132,
                    height: compact ? 72 : 100,
                    child: OverflowBox(
                      minWidth: 132,
                      maxWidth: 132,
                      minHeight: 100,
                      maxHeight: 100,
                      child: Transform.scale(
                        scale: compact ? 0.7 : 1,
                        child: RepaintBoundary(
                          child: ValueListenableBuilder<double>(
                            valueListenable: _clock,
                            builder: (context, t, _) => _Illustration(
                              kind: kind,
                              look: look,
                              icon: widget.icon ?? kind.icon,
                              t: t,
                              animated: _ticker.isActive,
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),
                  ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 320),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      spacing: 4,
                      children: [
                        _rise(
                          0,
                          Text(
                            widget.title,
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              color: AppColours.ink,
                              fontSize: compact ? 14 : 16,
                              fontWeight: FontWeight.w600,
                              height: 1.3,
                            ),
                          ),
                        ),
                        if (message != null && message.isNotEmpty)
                          _rise(
                            0.08,
                            Text(
                              message,
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                color: AppColours.inkMuted,
                                fontSize: compact ? 12 : 13,
                                height: 1.5,
                              ),
                            ),
                          ),
                      ],
                    ),
                  ),
                  if (hasAction)
                    _rise(
                      0.16,
                      // The FittedBox above gives its child no width limit, and
                      // a button's label is flexible.
                      ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 320),
                        child: OutlinedButton.icon(
                          onPressed: widget.onAction,
                          icon: Icon(kind.actionIcon, size: 17),
                          label: Text(actionLabel),
                          style: OutlinedButton.styleFrom(
                            minimumSize: const Size(0, 34),
                            padding: const EdgeInsets.symmetric(horizontal: 14),
                            foregroundColor: look.ink,
                            backgroundColor: look.tint,
                            side: BorderSide(color: look.border),
                            textStyle: const TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                            ),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(10),
                            ),
                          ),
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// What a list shows when it has nothing to show, told apart by why: a search
/// that matched nothing, filters that left nothing, or no rows at all. Texts
/// are passed in already localized.
class ListEmpty extends StatelessWidget {
  final bool hasSearch;
  final bool hasFilters;

  /// Title when the list is empty with no search or filter applied.
  final String noneTitle;
  final String? noneMessage;

  /// Title when a search or filters left nothing.
  final String noMatchTitle;
  final String? noMatchMessage;

  /// Message when a search matched nothing.
  final String searchMessage;

  final String? clearSearchLabel;
  final String? clearFiltersLabel;
  final VoidCallback? onClearSearch;
  final VoidCallback? onClearFilters;

  /// A smaller version for use inside a card or a short panel.
  final bool compact;

  const ListEmpty({
    super.key,
    required this.hasSearch,
    required this.hasFilters,
    required this.noneTitle,
    required this.noMatchTitle,
    required this.searchMessage,
    this.noneMessage,
    this.noMatchMessage,
    this.clearSearchLabel,
    this.clearFiltersLabel,
    this.onClearSearch,
    this.onClearFilters,
    this.compact = false,
  });

  @override
  Widget build(BuildContext context) {
    if (hasSearch) {
      return EmptyState(
        kind: EmptyKind.search,
        title: noMatchTitle,
        message: searchMessage,
        actionLabel: onClearSearch == null ? null : clearSearchLabel,
        onAction: onClearSearch,
        compact: compact,
      );
    }

    if (hasFilters) {
      return EmptyState(
        kind: EmptyKind.filter,
        title: noMatchTitle,
        message: noMatchMessage,
        actionLabel: onClearFilters == null ? null : clearFiltersLabel,
        onAction: onClearFilters,
        compact: compact,
      );
    }

    return EmptyState(
      title: noneTitle,
      message: noneMessage,
      compact: compact,
    );
  }
}

/// The accent of an [EmptyKind]: the tile and motion colour, the soft fill and
/// hairline of the action button and the text on it.
class _Look {
  final Color accent;
  final Color tint;
  final Color border;
  final Color ink;

  const _Look(this.accent, this.tint, this.border, this.ink);

  static _Look of(EmptyKind kind) => switch (kind) {
    EmptyKind.error => const _Look(
      AppColours.errorIcon,
      AppColours.errorBackground,
      AppColours.dangerBorder,
      AppColours.errorText,
    ),
    EmptyKind.done => const _Look(
      AppColours.successIcon,
      AppColours.successBackground,
      AppColours.successBorder,
      AppColours.successText,
    ),
    EmptyKind.locked => const _Look(
      AppColours.inkMuted,
      AppColours.surfaceMuted,
      AppColours.border,
      AppColours.ink,
    ),
    _ => const _Look(
      AppColours.primaryColor,
      AppColours.primaryTint,
      AppColours.border,
      AppColours.primaryDark,
    ),
  };
}

// -----------------------------------------------------------------------------
// Motion: a loop is a function of the clock [t] in seconds.
// -----------------------------------------------------------------------------

/// Where in its loop an animation of [period] seconds is, 0 to 1, started
/// [delay] seconds late.
double _phase(double t, double period, [double delay = 0]) =>
    ((t - delay) / period) % 1.0;

/// 0 to 1 and back over a loop, each way eased in and out.
double _pingPong(double t, double period, [double delay = 0]) {
  final p = _phase(t, period, delay);

  return Curves.easeInOut.transform(p < 0.5 ? p * 2 : 2 - p * 2);
}

/// The value of [stops] (position, value) at [p], eased in and out between
/// two stops.
double _keyframes(double p, List<(double, double)> stops) {
  for (var i = 1; i < stops.length; i++) {
    final (end, to) = stops[i];
    if (p <= end) {
      final (start, from) = stops[i - 1];
      final local = (p - start) / (end - start);

      return from + (to - from) * Curves.easeInOut.transform(local);
    }
  }

  return stops.last.$2;
}

double _radians(double degrees) => degrees * math.pi / 180;

/// The stack of cards with the kind's tile on top.
class _Illustration extends StatelessWidget {
  final EmptyKind kind;
  final _Look look;
  final IconData icon;
  final double t;
  final bool animated;

  const _Illustration({
    required this.kind,
    required this.look,
    required this.icon,
    required this.t,
    required this.animated,
  });

  @override
  Widget build(BuildContext context) {
    Widget backCard(double left, double top, double rotation, double lift) {
      return Positioned(
        left: left,
        top: top,
        child: Transform.translate(
          offset: Offset(0, -lift),
          child: Transform.rotate(
            angle: _radians(rotation),
            child: Container(
              width: 70,
              height: 50,
              decoration: BoxDecoration(
                color: AppColours.surfaceMuted,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: AppColours.border),
              ),
            ),
          ),
        ),
      );
    }

    final floatA = animated ? _pingPong(t, 4.2) : 0.0;
    final floatB = animated ? _pingPong(t, 4.6, 0.4) : 0.0;
    final floatCard = animated ? _pingPong(t, 3.6) : 0.0;

    return Stack(
      clipBehavior: Clip.none,
      children: [
        backCard(8, 34, -7 - 2 * floatA, 3 * floatA),
        backCard(54, 30, 6 + 2 * floatB, 4 * floatB),
        Positioned(
          left: 20,
          top: 32,
          child: Transform.translate(
            offset: Offset(0, -6 * floatCard),
            child: Container(
              width: 92,
              height: 62,
              padding: const EdgeInsets.fromLTRB(12, 24, 12, 9),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: AppColours.border),
                boxShadow: const [
                  BoxShadow(
                    color: Color(0x14101830),
                    blurRadius: 24,
                    offset: Offset(0, 10),
                  ),
                ],
              ),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.end,
                spacing: 5,
                children: [
                  for (final (i, width) in const [0.72, 0.92, 0.5].indexed)
                    _SkeletonLine(
                      widthFactor: width,
                      opacity: animated
                          ? 0.45 + 0.55 * _pingPong(t, 1.8, 0.2 * i)
                          : 1,
                    ),
                ],
              ),
            ),
          ),
        ),
        if (kind != EmptyKind.error && kind != EmptyKind.filter)
          Positioned(
            left: 16,
            top: -23,
            child: Transform.rotate(
              angle: animated ? 2 * math.pi * _phase(t, 9) : 0,
              child: SizedBox(
                width: 100,
                height: 100,
                child: Stack(
                  children: [
                    _Dot(left: 47, top: 0, size: 6, color: look.accent, opacity: .5),
                    _Dot(left: 92, top: 58, size: 4, color: look.accent, opacity: .35),
                    _Dot(left: 4, top: 68, size: 5, color: look.accent, opacity: .4),
                  ],
                ),
              ),
            ),
          ),
        if (kind == EmptyKind.filter)
          for (final (left, top, delay) in const [
            (56.0, -8.0, 0.0),
            (64.0, -14.0, 0.6),
            (72.0, -8.0, 1.2),
          ])
            _Drop(
              left: left,
              top: top,
              color: look.accent,
              p: animated ? _phase(t, 1.8, delay) : 0.5,
            ),
        if (animated) _Ring(accent: look.accent, p: _phase(t, 2.4)),
        _Tile(kind: kind, look: look, icon: icon, t: t, animated: animated),
      ],
    );
  }
}

class _SkeletonLine extends StatelessWidget {
  final double widthFactor;
  final double opacity;

  const _SkeletonLine({required this.widthFactor, required this.opacity});

  @override
  Widget build(BuildContext context) {
    return FractionallySizedBox(
      widthFactor: widthFactor,
      child: Opacity(
        opacity: opacity,
        child: Container(
          height: 5,
          decoration: BoxDecoration(
            color: AppColours.border,
            borderRadius: BorderRadius.circular(3),
          ),
        ),
      ),
    );
  }
}

class _Dot extends StatelessWidget {
  final double left, top, size, opacity;
  final Color color;

  const _Dot({
    required this.left,
    required this.top,
    required this.size,
    required this.color,
    required this.opacity,
  });

  @override
  Widget build(BuildContext context) {
    return Positioned(
      left: left,
      top: top,
      child: Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: color.withValues(alpha: opacity),
        ),
      ),
    );
  }
}

/// A drop that falls into the funnel of the filter kind.
class _Drop extends StatelessWidget {
  final double left, top, p;
  final Color color;

  const _Drop({
    required this.left,
    required this.top,
    required this.color,
    required this.p,
  });

  @override
  Widget build(BuildContext context) {
    final fall = Curves.easeIn.transform(p);
    final opacity = p < 0.25
        ? Curves.easeIn.transform(p / 0.25)
        : p < 0.75
        ? 1.0
        : 1 - Curves.easeIn.transform((p - 0.75) / 0.25);

    return Positioned(
      left: left,
      top: top + (-16 + 26 * fall),
      child: Opacity(
        opacity: opacity,
        child: Container(
          width: 5,
          height: 5,
          decoration: BoxDecoration(shape: BoxShape.circle, color: color),
        ),
      ),
    );
  }
}

/// The ring that swells and fades around the tile.
class _Ring extends StatelessWidget {
  final Color accent;
  final double p;

  const _Ring({required this.accent, required this.p});

  @override
  Widget build(BuildContext context) {
    final e = Curves.easeOut.transform(p);

    return Positioned(
      left: 43,
      top: 4,
      child: Opacity(
        opacity: 0.6 * (1 - e),
        child: Transform.scale(
          scale: 0.9 + 0.85 * e,
          child: Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: accent, width: 2),
            ),
          ),
        ),
      ),
    );
  }
}

/// The kind's glyph on a solid tile, moving the way the kind behaves: the
/// search one scans, the error one shakes, the done one breathes.
class _Tile extends StatelessWidget {
  final EmptyKind kind;
  final _Look look;
  final IconData icon;
  final double t;
  final bool animated;

  const _Tile({
    required this.kind,
    required this.look,
    required this.icon,
    required this.t,
    required this.animated,
  });

  @override
  Widget build(BuildContext context) {
    var dx = 0.0, dy = 0.0, angle = 0.0, scale = 1.0;

    if (animated) {
      switch (kind) {
        case EmptyKind.search:
          final e = _pingPong(t, 2.8);
          dx = -18 + 36 * e;
          dy = 3 * e;
          angle = _radians(-10 + 20 * e);
        case EmptyKind.error:
          angle = _radians(
            _keyframes(_phase(t, 3.2), const [
              (0.0, 0.0),
              (0.84, 0.0),
              (0.87, -12.0),
              (0.9, 10.0),
              (0.93, -6.0),
              (0.96, 3.0),
              (1.0, 0.0),
            ]),
          );
        case EmptyKind.done:
          scale = 1 + 0.06 * _pingPong(t, 2.4);
        default:
          dy = -6 * _pingPong(t, 3.6);
      }
    }

    return Positioned(
      left: 43,
      top: 4,
      child: Transform.translate(
        offset: Offset(dx, dy),
        child: Transform.rotate(
          angle: angle,
          child: Transform.scale(
            scale: scale,
            child: Container(
              width: 46,
              height: 46,
              decoration: BoxDecoration(
                color: look.accent,
                borderRadius: BorderRadius.circular(14),
                boxShadow: const [
                  BoxShadow(
                    color: Color(0x2E101830),
                    blurRadius: 18,
                    offset: Offset(0, 8),
                  ),
                ],
              ),
              child: Icon(icon, size: 24, color: Colors.white),
            ),
          ),
        ),
      ),
    );
  }
}
