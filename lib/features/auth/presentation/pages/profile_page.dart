import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/app_info.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/user.dart';
import '../../domain/entities/user_role.dart';
import '../bloc/auth_cubit.dart';
import '../bloc/profile_stats_cubit.dart';
import '../widgets/auth_labels.dart';
import '../widgets/profile_menu_section.dart';
import '../widgets/profile_role_card.dart';
import '../widgets/profile_stat_tiles.dart';

/// Name, role, this month's activity, role-specific settings, account
/// actions and sign out. Never pushed as a new route: a role shell with
/// its own bottom nav embeds this in place as a tab ([onClose] left
/// null, matching its siblings — see `CoordinatorRootPage`); a role
/// without one swaps it in for the home content instead, passing
/// [onClose] to swap back (see `RoleHomePage`).
///
/// A shell whose bar has a "More" slot embeds this as that screen and passes
/// [navigationSection] — a card of rows to its overflow screens — which shows
/// after the role's own card, ahead of the Account card.
class ProfilePage extends StatelessWidget {
  final VoidCallback? onClose;
  final Widget? navigationSection;

  const ProfilePage({super.key, this.onClose, this.navigationSection});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (context) => getIt<ProfileStatsCubit>()..start(context.read<AuthCubit>().user),
      child: _ProfileView(onClose: onClose, navigationSection: navigationSection),
    );
  }
}

class _ProfileView extends StatelessWidget {
  final VoidCallback? onClose;
  final Widget? navigationSection;

  const _ProfileView({required this.onClose, required this.navigationSection});

  void _comingSoon(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(t.comingSoon), behavior: SnackBarBehavior.floating));
  }

  Future<void> _confirmSignOut(BuildContext context) async {
    final t = AppLocalizations.of(context)!;
    final confirmed = await showAdaptiveDialog<bool>(
      context: context,
      builder: (context) => AlertDialog.adaptive(
        title: Text(t.signOutConfirmTitle),
        content: Text(t.signOutConfirmMessage),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(t.cancel),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            style: TextButton.styleFrom(foregroundColor: AppColours.errorIcon),
            child: Text(t.signOut),
          ),
        ],
      ),
    );
    if (confirmed == true && context.mounted) {
      await context.read<AuthCubit>().signOut();
    }
  }

  List<(int? value, String label)> _statsFor(
    UserRole role,
    ProfileStatsState stats,
    AppLocalizations t,
  ) => switch (role) {
    UserRole.coordinator => [
      (stats.quotesSentThisMonth, t.profileStatQuotesSent),
      (stats.acceptedThisMonth, t.profileStatAccepted),
      (stats.clientsCount, t.profileStatClients),
    ],
    UserRole.inspector => [
      (stats.inspectionsThisMonth, t.profileStatInspections),
      (stats.certificatesThisMonth, t.profileStatCertificates),
      (stats.returnedThisMonth, t.profileStatReturned),
    ],
    UserRole.technicalManager => [
      (stats.reviewedThisMonth, t.profileStatReviewed),
      (stats.returnedThisMonth, t.profileStatReturned),
      (stats.sentThisMonth, t.profileStatSent),
    ],
    UserRole.admin => const [],
  };

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final user = context.select((AuthCubit cubit) => cubit.user);
    if (user == null) return const SizedBox.shrink();

    return Scaffold(
      backgroundColor: AppColours.background,
      body: ListView(
        padding: EdgeInsets.zero,
        children: [
          _ProfileHeader(user: user, onEdit: () => _comingSoon(context), onClose: onClose),
          Padding(
            padding: EdgeInsets.fromLTRB(
              20,
              14,
              20,
              onClose == null ? AppLayout.bottomNavClearance : 28,
            ),
            child: Column(
              children: [
                BlocBuilder<ProfileStatsCubit, ProfileStatsState>(
                  builder: (context, stats) {
                    final tiles = _statsFor(user.role, stats, t);
                    return tiles.isEmpty
                        ? const SizedBox.shrink()
                        : ProfileStatTiles(stats: tiles);
                  },
                ),
                const SizedBox(height: 14),
                ProfileRoleCard(user: user, onComingSoon: () => _comingSoon(context)),
                if (user.role != UserRole.admin) const SizedBox(height: 14),
                if (navigationSection != null) ...[navigationSection!, const SizedBox(height: 14)],
                ProfileAccountSection(onComingSoon: () => _comingSoon(context)),
                const SizedBox(height: 14),
                ProfileHelpSection(onComingSoon: () => _comingSoon(context)),
                const SizedBox(height: 24),
                SizedBox(
                  height: 52,
                  width: double.infinity,
                  child: OutlinedButton.icon(
                    onPressed: () => _confirmSignOut(context),
                    icon: const Icon(Icons.logout_rounded, size: 18),
                    label: Text(t.signOut),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppColours.dangerText,
                      backgroundColor: Colors.white,
                      side: const BorderSide(color: AppColours.dangerBorder, width: 1.5),
                      textStyle: AppTextStyles.buttonLabel.copyWith(color: AppColours.dangerText),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                Text(
                  t.versionLabel(AppInfo.version.isEmpty ? '1.0.0' : AppInfo.version),
                  style: AppTextStyles.caption,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ProfileHeader extends StatelessWidget {
  final AppUser user;
  final VoidCallback onEdit;
  final VoidCallback? onClose;

  const _ProfileHeader({required this.user, required this.onEdit, required this.onClose});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      padding: EdgeInsets.fromLTRB(20, MediaQuery.paddingOf(context).top + 14, 20, 28),
      decoration: const BoxDecoration(
        color: AppColours.primaryColor,
        borderRadius: BorderRadius.vertical(bottom: Radius.circular(28)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              if (onClose != null) ...[
                Material(
                  color: Colors.white.withValues(alpha: 0.16),
                  shape: const CircleBorder(),
                  child: IconButton(
                    onPressed: onClose,
                    tooltip: t.back,
                    icon: const Icon(Icons.chevron_left_rounded, color: Colors.white),
                  ),
                ),
                const SizedBox(width: 4),
              ],
              Expanded(
                child: Text(
                  t.profile,
                  style: AppTextStyles.cardTitle.copyWith(color: Colors.white, fontSize: 20),
                ),
              ),
              SizedBox(
                height: 40,
                child: TextButton(
                  onPressed: onEdit,
                  style: TextButton.styleFrom(
                    backgroundColor: Colors.white.withValues(alpha: 0.16),
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  ),
                  child: Text(t.editAction),
                ),
              ),
            ],
          ),
          const SizedBox(height: 18),
          Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Stack(
                clipBehavior: Clip.none,
                children: [
                  CircleAvatar(
                    radius: 38,
                    backgroundColor: Colors.white,
                    child: Text(
                      user.initials,
                      style: const TextStyle(
                        fontSize: 26,
                        fontWeight: FontWeight.w800,
                        color: AppColours.primaryDark,
                      ),
                    ),
                  ),
                  Positioned(
                    right: -2,
                    bottom: -2,
                    child: Tooltip(
                      message: t.changePhotoAction,
                      child: Material(
                        color: Colors.white,
                        shape: const CircleBorder(
                          side: BorderSide(color: AppColours.primaryColor, width: 3),
                        ),
                        child: InkWell(
                          customBorder: const CircleBorder(),
                          onTap: onEdit,
                          child: const SizedBox(
                            width: 32,
                            height: 32,
                            child: Icon(Icons.camera_alt_outlined, size: 16, color: AppColours.primaryDark),
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      user.name,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.pageTitle.copyWith(color: Colors.white, fontSize: 20),
                    ),
                    const SizedBox(height: 8),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text(
                        user.role.label(t),
                        style: AppTextStyles.badge.copyWith(color: AppColours.primaryDark),
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      user.email,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.caption.copyWith(color: AppColours.onPrimaryMuted),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
