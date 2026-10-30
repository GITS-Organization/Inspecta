import '../../../auth/domain/entities/qualification.dart';
import '../../../auth/domain/entities/user.dart';
import '../../../auth/domain/entities/user_role.dart';

/// A row of `GET /inspectors`.
class InspectorModel {
  final String id;
  final String fullName;
  final String email;
  final List<String> qualifications;
  final bool isOnLeave;
  final DateTime? leaveUntil;

  const InspectorModel({
    required this.id,
    required this.fullName,
    required this.email,
    required this.qualifications,
    required this.isOnLeave,
    this.leaveUntil,
  });

  factory InspectorModel.fromApiJson(Map<String, dynamic> json) {
    final leaveUntil = json['leaveUntil'];
    return InspectorModel(
      id: json['id'] as String,
      fullName: json['fullName'] as String? ?? '',
      email: json['email'] as String? ?? '',
      qualifications: (json['qualifications'] as List<dynamic>? ?? const [])
          .whereType<String>()
          .toList(),
      isOnLeave: json['isOnLeave'] as bool? ?? false,
      leaveUntil: leaveUntil is String ? DateTime.tryParse(leaveUntil) : null,
    );
  }

  /// An inspector on leave with no end date stays on leave indefinitely.
  AppUser toEntity() => AppUser(
    id: id,
    name: fullName,
    email: email,
    role: UserRole.inspector,
    qualifications: [for (final name in qualifications) Qualification(name: name)],
    onLeaveUntil: isOnLeave ? (leaveUntil ?? DateTime(9999)) : null,
  );
}
