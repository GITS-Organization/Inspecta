import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../l10n/app_localizations.dart';

class SplashWelcomeTitle extends StatelessWidget {
  const SplashWelcomeTitle({super.key});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return RichText(
      text: TextSpan(
        style: const TextStyle(
          fontSize: 30,
          fontWeight: FontWeight.w800,
          height: 1.15,
          color: Colors.white,
        ),
        children: [
          TextSpan(text: '${t.splashWelcome} '),
          const TextSpan(
            text: 'Integrity',
            style: TextStyle(color: AppColours.amberOnPrimary),
          ),
        ],
      ),
    );
  }
}
