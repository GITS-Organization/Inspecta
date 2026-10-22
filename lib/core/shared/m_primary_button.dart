import 'package:flutter/material.dart';

import '../consts/app_colors.dart';
import '../consts/app_text_styles.dart';

/// Full-width filled blue button used for a screen's main action
/// ("Sign in", "Send reset link", ...). While [loading], it shows a spinner
/// and ignores taps.
class MPrimaryButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final bool loading;

  /// White fill with navy text, for a dark or frosted backdrop.
  final bool inverted;

  const MPrimaryButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.loading = false,
    this.inverted = false,
  });

  @override
  Widget build(BuildContext context) {
    final enabled = onPressed != null && !loading;
    return SizedBox(
      width: double.infinity,
      height: 54,
      child: FilledButton(
        onPressed: enabled ? onPressed : null,
        style: FilledButton.styleFrom(
          backgroundColor: inverted ? Colors.white : AppColours.primaryColor,
          disabledBackgroundColor: (inverted ? Colors.white : AppColours.primaryColor)
              .withValues(alpha: loading ? 1 : 0.5),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(14),
          ),
        ),
        child: loading
            ? SizedBox.square(
                dimension: 22,
                child: CircularProgressIndicator(
                  strokeWidth: 2.5,
                  color: inverted ? AppColours.navy : Colors.white,
                ),
              )
            : Text(
                label,
                style: AppTextStyles.buttonLabel.copyWith(
                  color: inverted ? AppColours.navy : Colors.white,
                ),
              ),
      ),
    );
  }
}
