import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/bloc/auth_cubit.dart';
import '../../../auth/presentation/pages/profile_page.dart';
import '../../../auth/presentation/widgets/auth_labels.dart';

/// Landing screen for the signed-in user's role (BR-01.2). Placeholder
/// until each role's list is built in features 02–07.
///
/// This role has no bottom-nav shell yet (unlike the coordinator's — see
/// `CoordinatorRootPage`), so Profile isn't a tab here either; tapping the
/// avatar swaps this screen's own content for [ProfilePage] in place
/// instead of pushing a new route, and [ProfilePage]'s own close button
/// swaps back.
class RoleHomePage extends StatefulWidget {
  const RoleHomePage({super.key});

  @override
  State<RoleHomePage> createState() => _RoleHomePageState();
}

class _RoleHomePageState extends State<RoleHomePage> {
  bool _showProfile = false;

  @override
  Widget build(BuildContext context) {
    if (_showProfile) {
      return ProfilePage(onClose: () => setState(() => _showProfile = false));
    }

    final t = AppLocalizations.of(context)!;
    final user = context.select((AuthCubit cubit) => cubit.user);
    if (user == null) return const SizedBox.shrink();

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: ListView(
          padding: EdgeInsets.zero,
          children: [
            Container(
              padding: EdgeInsets.fromLTRB(
                20,
                MediaQuery.paddingOf(context).top + 24,
                20,
                24,
              ),
              decoration: const BoxDecoration(
                color: AppColours.primaryColor,
                borderRadius: BorderRadius.vertical(
                  bottom: Radius.circular(28),
                ),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Tooltip(
                        message: t.profile,
                        child: Material(
                          color: Colors.white,
                          shape: const CircleBorder(),
                          child: InkWell(
                            customBorder: const CircleBorder(),
                            onTap: () => setState(() => _showProfile = true),
                            child: SizedBox.square(
                              dimension: 44,
                              child: Center(
                                child: Text(
                                  user.initials,
                                  style: const TextStyle(
                                    fontSize: 15,
                                    fontWeight: FontWeight.w700,
                                    color: AppColours.primaryDark,
                                  ),
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              t.homeGreeting(user.name),
                              overflow: TextOverflow.ellipsis,
                              style: AppTextStyles.emphasis.copyWith(
                                color: Colors.white,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            Text(
                              user.role.label(t),
                              style: AppTextStyles.caption.copyWith(
                                color: AppColours.onPrimaryMuted,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 18),
                  Text(
                    user.role.homeTitle(t),
                    style: AppTextStyles.pageTitle.copyWith(
                      color: Colors.white,
                      fontSize: 26,
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(20),
              child: MNotice(type: MNoticeType.info, message: t.homeComingSoon),
            ),
          ],
        ),
      ),
    );
  }
}
