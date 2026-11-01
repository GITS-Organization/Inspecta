import 'package:equatable/equatable.dart';

import 'qualification.dart';
import 'user_role.dart';

/// An internal staff member.
class AppUser extends Equatable {
  final String id;
  final String name;
  final String email;
  final String? phone;
  final UserRole role;

  /// Equipment categories an inspector may inspect (e.g. Lifts, Cranes),
  /// each with its own certificate expiry.
  final List<Qualification> qualifications;

  /// Stored signature image, for technical managers.
  final String? signatureImageId;
  final bool active;
  final DateTime? lastLogin;

  /// Feature 04: an inspector a coordinator has marked as on leave until
  /// this date. Null, or in the past, means not on leave.
  final DateTime? onLeaveUntil;

  const AppUser({
    required this.id,
    required this.name,
    required this.email,
    required this.role,
    this.phone,
    this.qualifications = const [],
    this.signatureImageId,
    this.active = true,
    this.lastLogin,
    this.onLeaveUntil,
  });

  bool isOnLeave({DateTime? now}) =>
      onLeaveUntil != null && onLeaveUntil!.isAfter(now ?? DateTime.now());

  /// Up to two initials for an avatar ("Karim Adel" -> "KA").
  String get initials {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty);
    final letters = parts.take(2).map((p) => p[0].toUpperCase()).join();
    return letters.isEmpty ? '?' : letters;
  }

  @override
  List<Object?> get props => [
    id,
    name,
    email,
    phone,
    role,
    qualifications,
    signatureImageId,
    active,
    lastLogin,
    onLeaveUntil,
  ];
}
