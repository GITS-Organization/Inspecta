import 'package:flutter/material.dart';

import '../consts/app_colors.dart';
import '../consts/app_text_styles.dart';
import 'network_painter.dart';

/// The app's screen header: a rounded pale-blue banner with the faint
/// network artwork behind it, holding the avatar + greeting row, a bell, the
/// screen [title], and an optional [bottom] (usually a search box).
class AppHeader extends StatelessWidget {
  /// Small caption above the greeting (the user's role).
  final String eyebrow;

  /// Line under the eyebrow (e.g. "Good morning, Mahmoud").
  final String greeting;

  final String initials;
  final VoidCallback? onOpenProfile;

  final String title;

  /// Shown on the bell as a red dot when above zero.
  final int unreadNotifications;
  final VoidCallback? onOpenNotifications;
  final String notificationsTooltip;

  /// Sits under the title, inside the banner.
  final Widget? bottom;

  const AppHeader({
    super.key,
    required this.eyebrow,
    required this.greeting,
    required this.initials,
    required this.title,
    required this.notificationsTooltip,
    this.onOpenProfile,
    this.onOpenNotifications,
    this.unreadNotifications = 0,
    this.bottom,
  });

  @override
  Widget build(BuildContext context) {
    return AppBanner(
      topInset: MediaQuery.paddingOf(context).top,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              _Avatar(initials: initials, onTap: onOpenProfile),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      eyebrow,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.caption.copyWith(
                        fontWeight: FontWeight.w500,
                        height: 16 / 12,
                        fontSize: 12,
                      ),
                    ),
                    Text(
                      greeting,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.subtitle.copyWith(height: 20 / 14),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              _BellButton(
                tooltip: notificationsTooltip,
                unread: unreadNotifications,
                onTap: onOpenNotifications,
              ),
            ],
          ),
          const SizedBox(height: 16),
          Text(title, style: AppTextStyles.pageTitle),
          if (bottom != null) ...[const SizedBox(height: 16), bottom!],
        ],
      ),
    );
  }
}

/// The pale-blue rounded banner behind every screen header, with the
/// animated network artwork. [topInset] is the status-bar height to clear
/// when the screen draws under it (0 when a `SafeArea` already did).
class AppBanner extends StatelessWidget {
  final Widget child;
  final double topInset;

  const AppBanner({super.key, required this.child, this.topInset = 0});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.fromLTRB(16, topInset + 16, 16, 0),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(24),
        child: DecoratedBox(
          decoration: const BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [AppColours.bannerStart, AppColours.bannerEnd],
            ),
          ),
          child: Stack(
            children: [
              const Positioned.fill(
                child: IgnorePointer(
                  child: AnimatedNetwork(
                    nodes: 12,
                    color: AppColours.primaryColor,
                    strength: 0.8,
                  ),
                ),
              ),
              Padding(padding: const EdgeInsets.all(20), child: child),
            ],
          ),
        ),
      ),
    );
  }
}

class _Avatar extends StatelessWidget {
  final String initials;
  final VoidCallback? onTap;

  const _Avatar({required this.initials, this.onTap});

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColours.navy,
      shape: const CircleBorder(),
      child: InkWell(
        customBorder: const CircleBorder(),
        onTap: onTap,
        child: SizedBox.square(
          dimension: 44,
          child: Center(
            child: Text(
              initials,
              style: const TextStyle(
                fontSize: 15,
                fontWeight: FontWeight.w700,
                color: Colors.white,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _BellButton extends StatelessWidget {
  final String tooltip;
  final int unread;
  final VoidCallback? onTap;

  const _BellButton({required this.tooltip, required this.unread, this.onTap});

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: tooltip,
      child: Semantics(
        button: true,
        label: tooltip,
        child: Material(
          color: Colors.white,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(14),
            side: const BorderSide(color: AppColours.border),
          ),
          child: InkWell(
            borderRadius: BorderRadius.circular(14),
            onTap: onTap,
            child: SizedBox.square(
              dimension: 44,
              child: Stack(
                alignment: Alignment.center,
                children: [
                  const Icon(
                    Icons.notifications_none_rounded,
                    size: 20,
                    color: AppColours.ink,
                  ),
                  if (unread > 0)
                    Positioned(
                      top: 9,
                      right: 10,
                      child: Container(
                        width: 10,
                        height: 10,
                        decoration: BoxDecoration(
                          color: AppColours.red,
                          shape: BoxShape.circle,
                          border: Border.all(color: Colors.white, width: 2),
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
