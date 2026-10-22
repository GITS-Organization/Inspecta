import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/app_info.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/widgets/app_header.dart';
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

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark,
      child: Scaffold(
      backgroundColor: AppColours.background,
      body: ListView(
        padding: EdgeInsets.zero,
        children: [
          _ProfileHeader(user: user, onEdit: () => _comingSoon(context), onClose: onClose),
          Padding(
            padding: EdgeInsets.fromLTRB(
              16,
              12,
              16,
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
                if (user.role != UserRole.admin) ...[
                  const SizedBox(height: 12),
                  ProfileRoleCard(user: user, onComingSoon: () => _comingSoon(context)),
                ],
                if (navigationSection != null) ...[const SizedBox(height: 12), navigationSection!],
                const SizedBox(height: 12),
                ProfileAccountSection(onComingSoon: () => _comingSoon(context)),
                const SizedBox(height: 12),
                ProfileHelpSection(onComingSoon: () => _comingSoon(context)),
                const SizedBox(height: 20),
                SizedBox(
                  height: 54,
                  width: double.infinity,
                  child: OutlinedButton.icon(
                    onPressed: () => _confirmSignOut(context),
                    icon: const Icon(Icons.logout_rounded, size: 18),
                    label: Text(t.signOut),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppColours.dangerText,
                      backgroundColor: Colors.white,
                      side: const BorderSide(color: AppColours.dangerBorder),
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
      ),
    );
  }
}

/// The banner: an optional back button, the screen title and an Edit
/// button, then who's signed in — navy avatar with a camera badge, name,
/// role pill and email.
class _ProfileHeader extends StatelessWidget {
  final AppUser user;
  final VoidCallback onEdit;
  final VoidCallback? onClose;

  const _ProfileHeader({required this.user, required this.onEdit, required this.onClose});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AppBanner(
      topInset: MediaQuery.paddingOf(context).top,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              if (onClose != null) ...[
                MBackButton(onPressed: onClose),
                const SizedBox(width: 12),
              ],
              Expanded(child: Text(t.profile, style: AppTextStyles.pageTitle)),
              Material(
                color: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(14),
                  side: const BorderSide(color: AppColours.border),
                ),
                child: InkWell(
                  borderRadius: BorderRadius.circular(14),
                  onTap: onEdit,
                  child: Container(
                    height: 44,
                    alignment: Alignment.center,
                    padding: const EdgeInsets.symmetric(horizontal: 18),
                    child: Text(
                      t.editAction,
                      style: AppTextStyles.buttonLabel.copyWith(fontSize: 14, color: AppColours.ink),
                    ),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),
          Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Stack(
                clipBehavior: Clip.none,
                children: [
                  Container(
                    width: 72,
                    height: 72,
                    alignment: Alignment.center,
                    decoration: const BoxDecoration(color: AppColours.navy, shape: BoxShape.circle),
                    child: Text(
                      user.initials,
                      style: const TextStyle(fontSize: 26, fontWeight: FontWeight.w700, color: Colors.white),
                    ),
                  ),
                  PositionedDirectional(
                    end: -4,
                    bottom: -4,
                    child: Tooltip(
                      message: t.changePhotoAction,
                      child: Material(
                        color: Colors.white,
                        shape: const CircleBorder(side: BorderSide(color: AppColours.border)),
                        child: InkWell(
                          customBorder: const CircleBorder(),
                          onTap: onEdit,
                          child: const SizedBox(
                            width: 30,
                            height: 30,
                            child: Icon(Icons.camera_alt_outlined, size: 15, color: AppColours.ink),
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
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.sectionTitle.copyWith(fontSize: 22, height: 28 / 22, letterSpacing: -0.22),
                    ),
                    const SizedBox(height: 6),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                      decoration: BoxDecoration(
                        color: AppColours.primaryTint,
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text(user.role.label(t), style: AppTextStyles.badge.copyWith(color: AppColours.primaryDark)),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      user.email,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.caption.copyWith(fontSize: 12, height: 16 / 12),
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
