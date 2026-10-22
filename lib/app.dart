import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:inspecta/core/builder/flow_builder.dart';
import 'package:inspecta/core/framework/app_cubit.dart';
import 'package:inspecta/core/framework/app_theme_mode.dart';
import 'package:inspecta/core/framework/mtoast.dart';
import 'package:inspecta/core/framework/responsive.dart';
import 'package:inspecta/app/router/auth_gate.dart';
import 'package:inspecta/features/auth/presentation/bloc/auth_cubit.dart';
import 'package:inspecta/injection.dart';
import 'package:inspecta/l10n/app_localizations.dart';

class App extends StatefulWidget {
  const App({super.key});

  @override
  State<App> createState() => _AppState();
}

class _AppState extends State<App> with WidgetsBindingObserver {
  final appCubit = getIt<AppCubit>();
  final authCubit = getIt<AuthCubit>();

  @override
  void initState() {
    WidgetsBinding.instance.addObserver(this);
    _login();
    authCubit.start();
    super.initState();
  }

  Future<void> _login() async {
    if (!appCubit.isFirstStartUp) {
      await appCubit.clear();
    }
  }

  @override
  void didChangePlatformBrightness() {
    appCubit.applyPlatformThemeMode();
    super.didChangePlatformBrightness();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MultiBlocProvider(
      providers: [
        BlocProvider.value(value: appCubit),
        BlocProvider.value(value: authCubit),
      ],
      child: MResponsiveWrapper(
        child: FlowBuilder<AppCubit>(
          builder: (context, state, cubit) {
            return MaterialApp(
              title: 'Integrity',
              theme: cubit.appThemeMode.themeData(cubit.locale.languageCode),
              debugShowCheckedModeBanner: false,
              scaffoldMessengerKey: MToast.messengerKey,
              locale: cubit.locale,
              supportedLocales: AppLocalizations.supportedLocales,
              localizationsDelegates: AppLocalizations.localizationsDelegates,
              builder: (context, child) {
                return MResponsiveWrapper.wrapper(
                  child: child!,
                  context: context,
                );
              },
              home: const AuthGate(),
            );
            
          },
        ),
      ),
    );
  }
}
