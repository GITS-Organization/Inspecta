import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/requests_tab.dart';

/// The inbox's New / Maybe toggle: a rounded control as tall as the search
/// box it sits beside, with the active segment filled.
class RequestsTabBar extends StatelessWidget {
  /// Matches `ISearchField`'s height so the two line up in the header.
  static const double height = 48;

  final RequestsTab selected;
  final ValueChanged<RequestsTab> onChanged;

  const RequestsTabBar({super.key, required this.selected, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      height: height,
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColours.border),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          for (final tab in RequestsTab.values)
            _Segment(
              label: _label(t, tab),
              selected: tab == selected,
              onTap: () => onChanged(tab),
            ),
        ],
      ),
    );
  }

  String _label(AppLocalizations t, RequestsTab tab) => switch (tab) {
    RequestsTab.newTab => t.tabNew,
    RequestsTab.maybe => t.tabMaybe,
  };
}

class _Segment extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _Segment({required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      selected: selected,
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          padding: const EdgeInsets.symmetric(horizontal: 12),
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: selected ? AppColours.primaryColor : Colors.transparent,
            borderRadius: BorderRadius.circular(10),
          ),
          child: Text(
            label,
            maxLines: 1,
            style: AppTextStyles.caption.copyWith(
              fontSize: 11,
              fontWeight: FontWeight.w600,
              color: selected ? Colors.white : AppColours.inkMuted,
            ),
          ),
        ),
      ),
    );
  }
}
