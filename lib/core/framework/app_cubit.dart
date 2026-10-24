import 'dart:math';

import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:inspecta/core/builder/flow_state.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../consts/exports.dart';
import 'app_preferences.dart';
import 'app_theme_mode.dart';

class AppCubit extends Cubit<FlowState> {
  final AppPreferences _preferences;

  late Locale locale;
  late AppThemeMode appThemeMode;
  late ValueNotifier<bool> isArSelected;
  late bool isDarkTheme;

  AppCubit(this._preferences) : super(FlowState()) {
    locale = Locale(_preferences.language);
    appThemeMode = _preferences.appThemeMode;
    isDarkTheme = appThemeMode.isDarkTheme;
    isArSelected = ValueNotifier(
      _preferences.language == Constants.languageCached,
    );
  }

  final _storage = const FlutterSecureStorage(
    aOptions: AndroidOptions(resetOnError: true),
    iOptions: IOSOptions(
      accessibility: KeychainAccessibility.first_unlock,
      synchronizable: true,
    ),
  );

  static int get randomInt => Random().nextInt(1000);

  String get selectedLang => _preferences.language;

  void changeLang(String lang) {
    locale = Locale(lang);
    _preferences.language = lang;
    isArSelected.value = _preferences.language == Constants.languageCached;
    emit(state.copyWith(data: randomInt));
  }

  void changeThemeMode(int index) {
    _preferences.appThemeMode = getAppThemeModeFromIndex(index);
    appThemeMode = _preferences.appThemeMode;
    emit(state.copyWith(data: randomInt));
  }

  void applyPlatformThemeMode() {
    if (appThemeMode == AppThemeMode.system) {
      emit(state.copyWith(data: randomInt));
    }
  }

  bool get isFirstStartUp => _preferences.isFirstStartUp;

  Future<void> clear() async {
    if (isFirstStartUp) await _storage.deleteAll();
  }
}
