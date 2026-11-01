import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';

// The building blocks of the request screen's body: one card per section
// (email, found in the email, details, equipment), rows with an icon tile,
// pills, and the sticky bar at the bottom.

/// One card of the request screen.
class RequestSection extends StatelessWidget {
  final Widget child;
  final Color color;

  /// Outline of the card; none when null.
  final Color? borderColor;
  final EdgeInsetsGeometry padding;

  const RequestSection({
    super.key,
    required this.child,
    this.color = Colors.white,
    this.borderColor,
    this.padding = const EdgeInsets.all(18),
  });

  @override
  Widget build(BuildContext context) {
    // A Material rather than a decorated box, so the ripple of a tappable row
    // inside is drawn above the card's fill.
    return Material(
      color: color,
      clipBehavior: Clip.antiAlias,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(22),
        side: borderColor == null ? BorderSide.none : BorderSide(color: borderColor!),
      ),
      child: SizedBox(
        width: double.infinity,
        child: Padding(padding: padding, child: child),
      ),
    );
  }
}

/// The heading of a [RequestSection].
class SectionTitle extends StatelessWidget {
  final String text;

  const SectionTitle(this.text, {super.key});

  @override
  Widget build(BuildContext context) {
    return Text(text, style: AppTextStyles.sectionTitle.copyWith(fontSize: 16, height: 22 / 16));
  }
}

/// The small muted label above a value.
TextStyle get rowLabelStyle => AppTextStyles.caption.copyWith(fontSize: 12, height: 16 / 12);

enum PillTone { info, success, warning, danger }

/// A small rounded label: "Matched", "Required", "2 items".
class StatusPill extends StatelessWidget {
  final String label;
  final PillTone tone;

  const StatusPill({super.key, required this.label, this.tone = PillTone.info});

  @override
  Widget build(BuildContext context) {
    final (background, foreground) = switch (tone) {
      PillTone.info => (AppColours.chipBlueBackground, AppColours.chipBlueText),
      PillTone.success => (AppColours.chipGreenBackground, AppColours.chipGreenText),
      PillTone.warning => (AppColours.chipAmberBackground, AppColours.chipAmberText),
      PillTone.danger => (AppColours.chipRedBackground, AppColours.chipRedText),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(999)),
      child: Text(label, style: AppTextStyles.badge.copyWith(color: foreground)),
    );
  }
}

/// A rounded square holding an icon.
class IconTile extends StatelessWidget {
  final IconData icon;
  final double size;
  final bool error;

  const IconTile({super.key, required this.icon, this.size = 40, this.error = false});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: error ? AppColours.errorBackground : AppColours.primaryTint,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Icon(
        icon,
        size: size >= 44 ? 22 : 20,
        color: error ? AppColours.errorIcon : AppColours.primaryDark,
      ),
    );
  }
}

/// One line of a details card: an icon tile, a label over its [value], and
/// whatever sits at the end. A tap target when [onTap] is given.
class DetailRow extends StatelessWidget {
  final IconData icon;
  final String label;
  final Widget value;
  final Widget? trailing;
  final VoidCallback? onTap;

  /// Draws the tile red: the row is a required part that is still missing.
  final bool error;

  const DetailRow({
    super.key,
    required this.icon,
    required this.label,
    required this.value,
    this.trailing,
    this.onTap,
    this.error = false,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: ConstrainedBox(
        constraints: const BoxConstraints(minHeight: 68),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
          child: Row(
            children: [
              IconTile(icon: icon, error: error),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(label, style: rowLabelStyle),
                    const SizedBox(height: 2),
                    value,
                  ],
                ),
              ),
              if (trailing != null) ...[
                const SizedBox(width: 8),
                trailing!,
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// The hairline between two [DetailRow]s.
class RowDivider extends StatelessWidget {
  const RowDivider({super.key});

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.symmetric(horizontal: 18),
      child: Divider(height: 1, thickness: 1, color: AppColours.borderSubtle),
    );
  }
}

/// A text button drawn as an underlined link.
class UnderlineLink extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final double fontSize;
  final double minHeight;

  const UnderlineLink({
    super.key,
    required this.label,
    required this.onPressed,
    this.fontSize = 14,
    this.minHeight = 44,
  });

  @override
  Widget build(BuildContext context) {
    return TextButton(
      onPressed: onPressed,
      style: TextButton.styleFrom(
        foregroundColor: AppColours.ink,
        padding: EdgeInsets.zero,
        minimumSize: Size(44, minHeight),
        tapTargetSize: MaterialTapTargetSize.shrinkWrap,
        textStyle: AppTextStyles.link.copyWith(
          fontSize: fontSize,
          fontWeight: FontWeight.w600,
          decoration: TextDecoration.underline,
        ),
      ),
      child: Text(label),
    );
  }
}

/// An item's facts in a grid of up to three columns (fewer facts share the
/// row between them): each a label over its value, or "Not set" while it has
/// none.
class ItemFacts extends StatelessWidget {
  final List<(String, String?)> facts;

  const ItemFacts({super.key, required this.facts});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return LayoutBuilder(
      builder: (context, constraints) {
        final columns = facts.length.clamp(1, 3);
        final width = (constraints.maxWidth - 8 * (columns - 1)) / columns;
        return Wrap(
          spacing: 8,
          runSpacing: 12,
          children: [
            for (final (label, value) in facts)
              SizedBox(
                width: width,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(label, style: rowLabelStyle, maxLines: 2, overflow: TextOverflow.ellipsis),
                    const SizedBox(height: 2),
                    Text(
                      value == null || value.trim().isEmpty ? t.notSet : value,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.cardTitle.copyWith(
                        fontSize: 14,
                        height: 20 / 14,
                        color: value == null || value.trim().isEmpty ? AppColours.inkMuted : AppColours.ink,
                      ),
                    ),
                  ],
                ),
              ),
          ],
        );
      },
    );
  }
}

/// The strip under an item's name saying whether it is one of the client's
/// registered machines.
class MachineLinkStrip extends StatelessWidget {
  final bool linked;
  final String label;

  /// A second line, e.g. the machine's make and model.
  final String? details;

  /// The link at the end ("Change"); none when null.
  final Widget? action;

  const MachineLinkStrip({
    super.key,
    required this.linked,
    required this.label,
    this.details,
    this.action,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsetsDirectional.fromSTEB(12, 8, action == null ? 12 : 4, 8),
      decoration: BoxDecoration(
        color: linked ? AppColours.successBackground : AppColours.surfaceMuted,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Icon(
            linked ? Icons.link_rounded : Icons.link_off_rounded,
            size: 18,
            color: linked ? AppColours.successIcon : AppColours.inkMuted,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  label,
                  style: AppTextStyles.caption.copyWith(
                    fontWeight: FontWeight.w600,
                    color: linked ? AppColours.successText : AppColours.inkSecondary,
                  ),
                ),
                if (details != null && details!.isNotEmpty)
                  Text(details!, style: AppTextStyles.caption.copyWith(color: AppColours.inkBody)),
              ],
            ),
          ),
          ?action,
        ],
      ),
    );
  }
}

/// The bar pinned under the page: white, ruled on top.
class RequestFooterBar extends StatelessWidget {
  final Widget child;

  const RequestFooterBar({super.key, required this.child});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(18, 14, 18, 16),
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: AppColours.borderSubtle)),
      ),
      child: child,
    );
  }
}

/// The main action of the footer bar: a tall navy button with a chevron.
class RequestCtaButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final bool loading;

  /// Whether what the button does can go ahead. Until it can, the button is
  /// drawn muted — but a tap still goes through, so the screen can point at
  /// what is missing.
  final bool ready;

  const RequestCtaButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.loading = false,
    this.ready = true,
  });

  @override
  Widget build(BuildContext context) {
    final enabled = onPressed != null && !loading;
    final foreground = ready ? Colors.white : AppColours.inkBody;
    return SizedBox(
      width: double.infinity,
      height: 56,
      child: FilledButton(
        onPressed: enabled ? onPressed : null,
        style: FilledButton.styleFrom(
          backgroundColor: ready ? AppColours.primaryColor : AppColours.border,
          disabledBackgroundColor: AppColours.primaryColor.withValues(alpha: loading ? 1 : 0.5),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        ),
        child: loading
            ? const SizedBox.square(
                dimension: 22,
                child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white),
              )
            : Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(label, style: AppTextStyles.buttonLabel.copyWith(fontSize: 16, color: foreground)),
                  const SizedBox(width: 8),
                  Icon(Icons.chevron_right_rounded, size: 20, color: foreground),
                ],
              ),
      ),
    );
  }
}

/// A rounded box with a dashed outline, for a place that is still empty.
class DashedBox extends StatelessWidget {
  final Widget child;
  final Color color;

  const DashedBox({super.key, required this.child, required this.color});

  @override
  Widget build(BuildContext context) {
    return CustomPaint(painter: _DashedBorderPainter(color), child: child);
  }
}

class _DashedBorderPainter extends CustomPainter {
  static const _radius = 16.0;
  static const _dash = 6.0;
  static const _gap = 5.0;

  final Color color;

  const _DashedBorderPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.5;
    final outline = Path()
      ..addRRect(RRect.fromRectAndRadius(Offset.zero & size, const Radius.circular(_radius)).deflate(0.75));
    for (final metric in outline.computeMetrics()) {
      var distance = 0.0;
      while (distance < metric.length) {
        canvas.drawPath(metric.extractPath(distance, distance + _dash), paint);
        distance += _dash + _gap;
      }
    }
  }

  @override
  bool shouldRepaint(_DashedBorderPainter oldDelegate) => oldDelegate.color != color;
}
