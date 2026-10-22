import 'package:flutter/material.dart';

import '../../l10n/app_localizations.dart';
import '../consts/app_colors.dart';

/// Square light-blue back button used at the top of pushed screens.
/// Pops the current route unless [onPressed] is given.
class MBackButton extends StatelessWidget {
  final VoidCallback? onPressed;
  final String? tooltip;

  const MBackButton({super.key, this.onPressed, this.tooltip});

  @override
  Widget build(BuildContext context) {
    return IconButton(
      onPressed: onPressed ?? () => Navigator.of(context).maybePop(),
      tooltip: tooltip ?? AppLocalizations.of(context)!.back,
      style: IconButton.styleFrom(
        fixedSize: const Size.square(44),
        backgroundColor: Colors.white,
        foregroundColor: AppColours.ink,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: const BorderSide(color: AppColours.border),
        ),
      ),
      // Flips automatically in RTL.
      icon: const Icon(Icons.chevron_left_rounded, size: 26),
    );
  }
}
