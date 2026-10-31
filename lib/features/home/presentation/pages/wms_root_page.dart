import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/bloc/auth_cubit.dart';
import '../../../auth/presentation/pages/profile_page.dart';
import '../../../auth/presentation/widgets/auth_labels.dart';

/// One tile on the WMS home screen.
class _WmsModule {
  final IconData icon;
  final String Function(AppLocalizations t) label;

  const _WmsModule(this.icon, this.label);
}

/// The WMS modules, shared by finance, the managing director and the
/// merchandiser. Add a module here (and its label to the `.arb` files).
final _modules = <_WmsModule>[
  _WmsModule(Icons.inventory_2_outlined, (t) => t.wmsModuleInventory),
  _WmsModule(Icons.move_to_inbox_outlined, (t) => t.wmsModuleReceiving),
  _WmsModule(Icons.shopping_basket_outlined, (t) => t.wmsModulePicking),
  _WmsModule(Icons.local_shipping_outlined, (t) => t.wmsModuleShipping),
];

/// Home of the finance, managing director and merchandiser roles: the WMS
/// modules. Like `RoleHomePage`, it has no bottom-nav shell, so tapping the
/// avatar swaps its content for [ProfilePage] in place.
class WmsRootPage extends StatefulWidget {
  const WmsRootPage({super.key});

  @override
  State<WmsRootPage> createState() => _WmsRootPageState();
}

class _WmsRootPageState extends State<WmsRootPage> {
  bool _showProfile = false;

  void _comingSoon(AppLocalizations t) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(t.comingSoon),
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

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
              child: GridView.count(
                crossAxisCount: 2,
                mainAxisSpacing: 12,
                crossAxisSpacing: 12,
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                children: [
                  for (final module in _modules)
                    _ModuleTile(
                      icon: module.icon,
                      label: module.label(t),
                      onTap: () => _comingSoon(t),
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

class _ModuleTile extends StatelessWidget {
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  const _ModuleTile({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(20),
      child: InkWell(
        borderRadius: BorderRadius.circular(20),
        onTap: onTap,
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, size: 36, color: AppColours.primaryColor),
            const SizedBox(height: 12),
            Text(
              label,
              textAlign: TextAlign.center,
              style: AppTextStyles.emphasis.copyWith(color: AppColours.ink),
            ),
          ],
        ),
      ),
    );
  }
}
