import 'package:flutter/material.dart';

import '../consts/app_colors.dart';

/// Short floating messages, shown without needing a [BuildContext].
/// Requires [messengerKey] to be set as the `MaterialApp`'s
/// `scaffoldMessengerKey`.
class MToast {
  MToast._();

  static final messengerKey = GlobalKey<ScaffoldMessengerState>();

  static void showSuccess({required String message}) {
    _show(message: message, backgroundColor: AppColours.successIcon);
  }

  static void showError({required String message}) {
    _show(message: message, backgroundColor: AppColours.errorIcon);
  }

  static void _show({required String message, required Color backgroundColor}) {
    final messenger = messengerKey.currentState;
    if (messenger == null) return;
    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(message, style: const TextStyle(color: Colors.white)),
          backgroundColor: backgroundColor,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(14),
          ),
        ),
      );
  }
}
