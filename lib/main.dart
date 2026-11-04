import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:inspecta/app.dart';
import 'package:inspecta/core/framework/app_info.dart';
import 'package:inspecta/core/framework/orientation_helper.dart';
import 'package:inspecta/injection.dart';

Future<void> main() async {
  runZonedGuarded<Future<void>>(
    () async {
      WidgetsFlutterBinding.ensureInitialized();

      // No status-bar background: screens draw behind it, and each one's
      // AnnotatedRegion picks the icon brightness.
      SystemChrome.setSystemUIOverlayStyle(
        const SystemUiOverlayStyle(statusBarColor: Colors.transparent, systemStatusBarContrastEnforced: false),
      );

      await OrientationHelper.lockToPortrait();
      await AppInfo.init();
      await setupDependencies();

      FlutterError.onError = FlutterError.presentError;

      runApp(const App());
    },
    (error, stack) {
      debugPrint('Uncaught error: $error\n$stack');
    },
  );
}
