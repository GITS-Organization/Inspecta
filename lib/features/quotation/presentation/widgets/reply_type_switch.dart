import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/reply_type.dart';

/// The three reply choices (BR-03.1) as one segmented control — Accept, Send
/// offer, Reject — with the chosen one's explanation in a line beneath it, so
/// the form below stays on screen. Reject reads in red, and fills red when it
/// is the one chosen.
class ReplyTypeSwitch extends StatelessWidget {
  static const double _height = 48;

  /// `null` until the coordinator has picked one.
  final ReplyType? selected;
  final ValueChanged<ReplyType> onChanged;

  const ReplyTypeSwitch({super.key, required this.selected, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final options = [
      (type: ReplyType.accept, label: t.replyAccept, subtitle: t.replyAcceptSubtitle),
      (type: ReplyType.offer, label: t.replyOfferShort, subtitle: t.replyOfferSubtitle),
      (type: ReplyType.reject, label: t.replyReject, subtitle: t.replyRejectSubtitle),
    ];
    final current = options.where((option) => option.type == selected).firstOrNull;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          height: _height,
          padding: const EdgeInsets.all(4),
          decoration: BoxDecoration(
            color: AppColours.surfaceMuted,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: AppColours.border),
          ),
          child: Row(
            children: [
              for (final option in options)
                Expanded(
                  child: _Segment(
                    label: option.label,
                    selected: option.type == selected,
                    danger: option.type == ReplyType.reject,
                    onTap: () => onChanged(option.type),
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        Text(current?.subtitle ?? t.chooseReplyTypeFirst, style: AppTextStyles.subtitle),
      ],
    );
  }
}

class _Segment extends StatelessWidget {
  final String label;
  final bool selected;
  final bool danger;
  final VoidCallback onTap;

  const _Segment({
    required this.label,
    required this.selected,
    required this.danger,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final fill = danger ? AppColours.errorIcon : AppColours.primaryColor;
    final foreground = selected
        ? Colors.white
        : danger
        ? AppColours.errorText
        : AppColours.ink;

    return Semantics(
      button: true,
      selected: selected,
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: AnimatedContainer(
          duration: MediaQuery.disableAnimationsOf(context) ? Duration.zero : const Duration(milliseconds: 180),
          curve: Curves.easeOutCubic,
          alignment: Alignment.center,
          padding: const EdgeInsets.symmetric(horizontal: 8),
          decoration: BoxDecoration(
            color: selected ? fill : Colors.transparent,
            borderRadius: BorderRadius.circular(12),
          ),
          child: Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.caption.copyWith(
              fontSize: 13.5,
              height: 1,
              fontWeight: FontWeight.w600,
              color: foreground,
            ),
          ),
        ),
      ),
    );
  }
}
