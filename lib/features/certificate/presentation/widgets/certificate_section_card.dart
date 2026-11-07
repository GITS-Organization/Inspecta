import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';

/// Every step's shared card shape: a numbered (or checked, once done)
/// circle, a title/subtitle, and a blue border while it's the current
/// step (Feature 05).
///
/// The header ends in an arrow: a tap on it (or anywhere on the header) opens
/// the card with its data and folds it down to its title again. It starts
/// folded. A folded card keeps its fields alive, only hidden, so what was typed
/// in them is not lost and nothing is read again when it opens.
class CertificateSectionCard extends StatefulWidget {
  final int stepNumber;
  final bool highlighted;
  final bool done;
  final String title;
  final String? subtitle;
  final Widget child;

  const CertificateSectionCard({
    super.key,
    required this.stepNumber,
    required this.highlighted,
    required this.done,
    required this.title,
    this.subtitle,
    required this.child,
  });

  @override
  State<CertificateSectionCard> createState() => _CertificateSectionCardState();
}

class _CertificateSectionCardState extends State<CertificateSectionCard> with SingleTickerProviderStateMixin {
  static const _duration = Duration(milliseconds: 220);

  late final AnimationController _controller = AnimationController(vsync: this, duration: _duration, value: 0);
  late final CurvedAnimation _size = CurvedAnimation(
    parent: _controller,
    curve: Curves.easeOutCubic,
    reverseCurve: Curves.easeInCubic,
  );

  bool _open = false;

  @override
  void dispose() {
    _size.dispose();
    _controller.dispose();
    super.dispose();
  }

  void _toggle() {
    // With animations turned off the card simply opens or closes.
    _controller.duration = MediaQuery.disableAnimationsOf(context) ? Duration.zero : _duration;
    setState(() => _open = !_open);
    if (_open) {
      _controller.forward();
    } else {
      _controller.reverse();
    }
  }

  @override
  Widget build(BuildContext context) {
    final highlighted = widget.highlighted;
    final reduceMotion = MediaQuery.disableAnimationsOf(context);

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: highlighted ? Border.all(color: AppColours.primaryColor, width: 1.5) : null,
        boxShadow: [
          BoxShadow(color: AppColours.ink.withValues(alpha: 0.05), blurRadius: 16, offset: const Offset(0, 6)),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Semantics(
            button: true,
            expanded: _open,
            child: Material(
              type: MaterialType.transparency,
              child: InkWell(
                borderRadius: BorderRadius.circular(12),
                onTap: _toggle,
                child: Row(
                  children: [
                    Container(
                      width: 28,
                      height: 28,
                      alignment: Alignment.center,
                      decoration: BoxDecoration(
                        color: widget.done
                            ? AppColours.successBackground
                            : (highlighted ? AppColours.primaryColor : AppColours.surfaceMuted),
                        shape: BoxShape.circle,
                      ),
                      child: widget.done
                          ? const Icon(Icons.check_rounded, size: 16, color: AppColours.successIcon)
                          : Text(
                              '${widget.stepNumber}',
                              style: TextStyle(
                                fontSize: 13,
                                fontWeight: FontWeight.w800,
                                color: highlighted ? Colors.white : AppColours.inkMuted,
                              ),
                            ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(widget.title, style: AppTextStyles.cardTitle.copyWith(fontSize: 15)),
                          if (widget.subtitle != null)
                            Text(widget.subtitle!, style: AppTextStyles.subtitle.copyWith(fontSize: 13)),
                        ],
                      ),
                    ),
                    const SizedBox(width: 8),
                    // Down when it is closed (tap to open), up when it is open.
                    AnimatedRotation(
                      turns: _open ? 0.5 : 0,
                      duration: reduceMotion ? Duration.zero : _duration,
                      curve: Curves.easeOutCubic,
                      child: const Icon(Icons.keyboard_arrow_down_rounded, size: 26, color: AppColours.inkMuted),
                    ),
                  ],
                ),
              ),
            ),
          ),
          SizeTransition(
            sizeFactor: _size,
            axisAlignment: -1,
            child: ExcludeFocus(
              excluding: !_open,
              child: ExcludeSemantics(
                excluding: !_open,
                child: Padding(padding: const EdgeInsets.only(top: 16), child: widget.child),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
