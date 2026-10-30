import 'package:flutter/material.dart';

import '../consts/app_colors.dart';
import '../consts/app_text_styles.dart';

enum MNoticeType { info, success, error }

/// Tinted panel with an icon and a short message, shown inline in a form
/// (the sign-in hint, "Check your inbox", a sign-in error, ...).
class MNotice extends StatelessWidget {
  final MNoticeType type;
  final String message;
  final String? title;

  const MNotice({
    super.key,
    required this.type,
    required this.message,
    this.title,
  });

  @override
  Widget build(BuildContext context) {
    final (background, iconColor, textColor, icon) = switch (type) {
      MNoticeType.info => (
        AppColours.surfaceMuted,
        AppColours.primaryDark,
        AppColours.inkBody,
        Icons.info_outline_rounded,
      ),
      MNoticeType.success => (
        AppColours.successBackground,
        AppColours.successIcon,
        AppColours.successText,
        Icons.check_rounded,
      ),
      MNoticeType.error => (
        AppColours.errorBackground,
        AppColours.errorIcon,
        AppColours.errorText,
        Icons.error_outline_rounded,
      ),
    };
    final style = AppTextStyles.caption.copyWith(color: textColor);

    return Semantics(
      liveRegion: type != MNoticeType.info,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: background,
          borderRadius: BorderRadius.circular(14),
        ),
        child: Row(
          crossAxisAlignment: title != null
              ? CrossAxisAlignment.start
              : CrossAxisAlignment.center,
          children: [
            Icon(icon, size: 20, color: iconColor),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (title != null)
                    Text(
                      title!,
                      style: style.copyWith(fontWeight: FontWeight.w700),
                    ),
                  Text(message, style: style),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
