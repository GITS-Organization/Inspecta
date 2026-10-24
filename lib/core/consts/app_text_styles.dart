import 'package:flutter/material.dart';

import 'app_colors.dart';

/// Shared text styles used across the app. Prefer these over inline
/// [TextStyle] literals; use [TextStyle.copyWith] for one-off variations
/// (a different size, an accent colour) rather than writing a new style.
class AppTextStyles {
  AppTextStyles._();

  /// Large bold screen heading (e.g. "Welcome back", "Reset your password").
  static const TextStyle pageTitle = TextStyle(
    fontSize: 32,
    height: 38 / 32,
    fontWeight: FontWeight.w700,
    letterSpacing: -0.64,
    color: AppColours.ink,
  );

  /// Heading above a list section (e.g. "Needs your reply").
  static const TextStyle sectionTitle = TextStyle(
    fontSize: 18,
    height: 24 / 18,
    fontWeight: FontWeight.w600,
    color: AppColours.ink,
  );

  /// Heading for a card or list item (e.g. a client's name).
  static const TextStyle cardTitle = TextStyle(
    fontSize: 15,
    height: 22 / 15,
    fontWeight: FontWeight.w600,
    color: AppColours.ink,
  );

  /// Paragraph copy under a page title.
  static const TextStyle body = TextStyle(
    fontSize: 15,
    height: 1.55,
    color: AppColours.inkSecondary,
  );

  /// Muted secondary text under a heading.
  static const TextStyle subtitle = TextStyle(
    fontSize: 14,
    color: AppColours.inkSecondary,
  );

  /// Smaller muted text: hints, captions, timestamps, info panels.
  static const TextStyle caption = TextStyle(
    fontSize: 13,
    height: 1.5,
    color: AppColours.inkMuted,
  );

  /// Bold label above a form field.
  static const TextStyle fieldLabel = TextStyle(
    fontSize: 14,
    fontWeight: FontWeight.w600,
    color: AppColours.ink,
  );

  /// Text typed into an [MTextField] (email, password, phone, ...).
  static const TextStyle input = TextStyle(
    fontSize: 16,
    fontWeight: FontWeight.w500,
    color: AppColours.ink,
  );

  /// Bold text link (e.g. "Forgot password?").
  static const TextStyle link = TextStyle(
    fontSize: 14,
    fontWeight: FontWeight.w700,
    color: AppColours.primaryDark,
  );

  /// Semi-bold text for a stat or short piece of emphasis copy.
  static const TextStyle emphasis = TextStyle(
    fontSize: 16,
    fontWeight: FontWeight.w600,
    color: AppColours.ink,
  );

  /// Bold white label for a filled primary button.
  static const TextStyle buttonLabel = TextStyle(
    fontSize: 15,
    fontWeight: FontWeight.w700,
    letterSpacing: -0.3,
    color: Colors.white,
  );

  /// Small label for a status badge or priority chip; colour is applied
  /// per-instance with [TextStyle.copyWith].
  static const TextStyle badge = TextStyle(
    fontSize: 12,
    height: 16 / 12,
    fontWeight: FontWeight.w600,
  );
}
