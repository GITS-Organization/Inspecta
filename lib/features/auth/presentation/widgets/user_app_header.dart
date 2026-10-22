import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/widgets/app_header.dart';
import '../../../../l10n/app_localizations.dart';
import '../bloc/auth_cubit.dart';
import 'auth_labels.dart';

/// [AppHeader] filled in for whoever is signed in: their initials, role,
/// a time-of-day greeting with their first name, and the bell. Each role's
/// list screen supplies only its [title] and what sits under it.
class UserAppHeader extends StatelessWidget {
  final String title;
  final VoidCallback? onOpenProfile;
  final Widget? bottom;

  const UserAppHeader({super.key, required this.title, this.onOpenProfile, this.bottom});

  static String _greeting(AppLocalizations t) {
    final hour = DateTime.now().hour;
    if (hour < 12) return t.requestsGreetingMorning;
    if (hour < 18) return t.requestsGreetingAfternoon;
    return t.requestsGreetingEvening;
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final user = context.select((AuthCubit cubit) => cubit.user);
    if (user == null) return const SizedBox.shrink();

    final comma = Localizations.localeOf(context).languageCode == 'ar' ? '، ' : ', ';
    final firstName = user.name.trim().split(RegExp(r'\s+')).first;

    return AppHeader(
      eyebrow: user.role.label(t),
      greeting: '${_greeting(t)}$comma$firstName',
      initials: user.initials,
      onOpenProfile: onOpenProfile,
      notificationsTooltip: t.notifications,
      title: title,
      bottom: bottom,
    );
  }
}
