import 'package:flutter/material.dart';

import '../consts/app_colors.dart';
import '../consts/app_text_styles.dart';

/// One destination on [AppBottomNav].
class AppNavItem {
  final IconData icon;
  final String label;

  const AppNavItem({required this.icon, required this.label});
}

/// The role shells' floating navy bar: a full-width, fully rounded dark pill.
/// The active destination is a white pill holding its icon and label; every
/// other destination is just its icon. Switching destinations animates — the
/// white pill's width slides from one tile to the next while its label
/// fades and grows in. Pass it as a `Scaffold`'s `bottomNavigationBar`; it
/// adds its own margin and safe-area padding.
class AppBottomNav extends StatelessWidget {
  /// The selected tile's share of the bar's inner width.
  static const _selectedShare = 0.44;

  final List<AppNavItem> items;
  final int selectedIndex;
  final ValueChanged<int> onSelect;

  const AppBottomNav({
    super.key,
    required this.items,
    required this.selectedIndex,
    required this.onSelect,
  });

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      top: false,
      minimum: const EdgeInsets.only(bottom: 12),
      child: Container(
        margin: const EdgeInsets.symmetric(horizontal: 16),
        padding: const EdgeInsets.all(8),
        decoration: BoxDecoration(
          color: AppColours.navy,
          borderRadius: BorderRadius.circular(999),
          boxShadow: [
            BoxShadow(
              color: AppColours.navy.withValues(alpha: 0.28),
              blurRadius: 24,
              offset: const Offset(0, 8),
            ),
          ],
        ),
        child: LayoutBuilder(
          builder: (context, constraints) {
            // Every tile has an explicit width (so the change can animate):
            // the selected one takes its share, the rest split what's left.
            final selectedWidth = constraints.maxWidth * _selectedShare;
            final otherWidth = items.length > 1
                ? (constraints.maxWidth - selectedWidth) / (items.length - 1)
                : constraints.maxWidth;
            return Row(
              children: [
                for (var i = 0; i < items.length; i++)
                  _NavTile(
                    item: items[i],
                    selected: i == selectedIndex,
                    width: i == selectedIndex ? selectedWidth : otherWidth,
                    onTap: () => onSelect(i),
                  ),
              ],
            );
          },
        ),
      ),
    );
  }
}

class _NavTile extends StatelessWidget {
  static const _duration = Duration(milliseconds: 280);
  static const _curve = Curves.easeOutCubic;

  final AppNavItem item;
  final bool selected;
  final double width;
  final VoidCallback onTap;

  const _NavTile({
    required this.item,
    required this.selected,
    required this.width,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      selected: selected,
      label: item.label,
      child: Tooltip(
        message: selected ? '' : item.label,
        child: GestureDetector(
          onTap: onTap,
          behavior: HitTestBehavior.opaque,
          child: AnimatedContainer(
            duration: _duration,
            curve: _curve,
            width: width,
            height: 46,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: selected ? Colors.white : Colors.transparent,
              borderRadius: BorderRadius.circular(999),
            ),
            // Scales the icon + label down rather than overflowing, if a
            // long label (or a larger system font) doesn't fit its tile.
            child: FittedBox(
              fit: BoxFit.scaleDown,
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TweenAnimationBuilder<Color?>(
                    tween: ColorTween(end: selected ? AppColours.navy : AppColours.onNavyMuted),
                    duration: _duration,
                    builder: (context, color, _) => Icon(item.icon, size: 22, color: color),
                  ),
                  // The label's width animates from zero, so the pill grows
                  // around it instead of the text popping in.
                  ClipRect(
                    child: AnimatedAlign(
                      duration: _duration,
                      curve: _curve,
                      alignment: AlignmentDirectional.centerStart,
                      widthFactor: selected ? 1 : 0,
                      child: AnimatedOpacity(
                        duration: _duration,
                        curve: _curve,
                        opacity: selected ? 1 : 0,
                        child: Padding(
                          padding: const EdgeInsetsDirectional.only(start: 8),
                          child: Text(
                            item.label,
                            maxLines: 1,
                            softWrap: false,
                            style: AppTextStyles.caption.copyWith(
                              fontSize: 14,
                              height: 1,
                              fontWeight: FontWeight.w700,
                              color: AppColours.navy,
                            ),
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
