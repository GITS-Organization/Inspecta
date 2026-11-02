/// Shared layout measurements used across features.
class AppLayout {
  AppLayout._();

  /// Space a scrollable tab leaves at its bottom, under its last card. The
  /// root bottom nav is a bar of its own, so nothing hides behind it.
  static const double bottomNavClearance = 24;
}
