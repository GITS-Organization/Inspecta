import 'package:flutter/material.dart';

/// Colour tokens from the "Requests inbox" design (navy on a cool grey
/// page). The names predate the redesign, so most of them keep their old
/// role and just carry the new value.
class AppColours {
  AppColours._();

  // ---- Brand ----
  /// Deep navy: the avatar, the floating bottom bar, and — as the app's
  /// primary colour — buttons, selected chips/toggles and the progress
  /// indicator.
  static const Color navy = Color(0xFF0B1B3F);

  static const Color primaryColor = navy;

  /// Slightly lighter navy for icons and links on light surfaces.
  static const Color primaryDark = Color(0xFF11295D);

  /// Light blue-grey behind an icon tile, a count pill, or a focused
  /// input's ring.
  static const Color primaryTint = Color(0xFFE4EDFB);

  /// Hover/pressed wash for a row or a selected tile.
  static const Color primarySoft = Color(0xFFECF2FC);

  /// Text/icons on [navy] when not selected.
  static const Color onNavyMuted = Color(0xFFC3CEE8);

  /// Muted text on top of a [primaryColor] panel.
  static const Color onPrimaryMuted = Color(0xFFC3CEE8);

  /// Soft yellow for an emphasised number/icon on a [primaryColor] panel
  /// — a warning accent that still reads on a dark background, unlike
  /// [chipAmberText].
  static const Color amberOnPrimary = Color(0xFFFDE68A);

  /// Primary buttons and focused inputs on auth screens.
  static const Color authAccent = primaryColor;

  // ---- Header banner gradient (top-left → bottom-right) ----
  static const Color bannerStart = Color(0xFFF8FAFE);
  static const Color bannerEnd = Color(0xFFE5EEFB);

  // ---- Text ----
  static const Color ink = Color(0xFF0F1B33);
  static const Color inkBody = Color(0xFF3A4660);
  static const Color inkSecondary = Color(0xFF56617A);
  static const Color inkMuted = Color(0xFF56617A);

  // ---- Surfaces ----
  /// Cool grey page behind the cards on the role screens.
  static const Color background = Color(0xFFEDF1F7);

  /// Filled chip / info panel / icon button background.
  static const Color surfaceMuted = Color(0xFFF5F7FB);

  /// Resting border of an input or an outlined button.
  static const Color border = Color(0xFFCFD7E6);

  /// Hairline: tab-bar rule, dividers.
  static const Color borderSubtle = Color(0xFFE4E9F2);

  // ---- Status ----
  static const Color grey = Color(0xff9E9E9E);
  static const Color green = Color(0xff34C77B);
  static const Color red = Color(0xffD43A30);

  static const Color successBackground = Color(0xFFE2F4EA);
  static const Color successIcon = Color(0xFF1A8F4E);
  static const Color successText = Color(0xFF0F6B3A);

  /// Soft red for an error message or border on a dark/frosted surface.
  static const Color glassError = Color(0xFFFF9C95);

  /// Green that reads on a [navy] surface.
  static const Color successOnNavy = Color(0xFF5ED299);

  static const Color errorBackground = Color(0xFFFCE8E6);
  static const Color errorIcon = Color(0xFFD43A30);
  static const Color errorText = Color(0xFFA12A22);

  /// Border for an outlined "positive" button (e.g. "Accept client's
  /// price") — pair with [successIcon] for its text/icon colour.
  static const Color successBorder = Color(0xFF8FD4AE);

  /// Border/text for an outlined "danger" button (e.g. "Mark declined") —
  /// distinct from [errorBackground]'s chip colours, which read too dark
  /// on an outlined white button.
  static const Color dangerBorder = Color(0xFFEBA19B);
  static const Color dangerText = Color(0xFFA12A22);

  // ---- Status chips (job status badges on request/job cards) ----
  static const Color chipAmberBackground = Color(0xFFFDF1D6);
  static const Color chipAmberText = Color(0xFF7A4E00);

  /// Deeper amber for the emphasised value under a [chipAmberText] label
  /// (e.g. the client's counter-price figure) — darker than the label so
  /// the number reads as the important part.
  static const Color chipAmberTextStrong = Color(0xFF5C3A00);
  static const Color chipBlueBackground = primaryTint;
  static const Color chipBlueText = primaryDark;
  static const Color chipGreenBackground = successBackground;
  static const Color chipGreenText = successText;
  static const Color chipRedBackground = errorBackground;
  static const Color chipRedText = errorText;
  static const Color chipGreyBackground = borderSubtle;
  static const Color chipGreyText = inkMuted;
}
