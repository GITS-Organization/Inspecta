import '../../domain/entities/qualification.dart';
import '../../domain/entities/user.dart';
import '../../domain/entities/user_role.dart';

/// The `auth/login` response: the token pair plus the signed-in user. It is
/// also what is kept in secure storage, so a launch can restore the session.
class AuthSessionModel {
  final String accessToken;
  final String refreshToken;

  /// When [accessToken] stops being accepted.
  final DateTime expiresAt;
  final AuthUserModel user;

  const AuthSessionModel({
    required this.accessToken,
    required this.refreshToken,
    required this.expiresAt,
    required this.user,
  });

  factory AuthSessionModel.fromJson(Map<String, dynamic> json) {
    return AuthSessionModel(
      accessToken: json['accessToken'] as String,
      refreshToken: json['refreshToken'] as String,
      expiresAt: DateTime.parse(json['expiresAt'] as String),
      user: AuthUserModel.fromJson(json['user'] as Map<String, dynamic>),
    );
  }

  Map<String, dynamic> toJson() =>
      {
        'accessToken': accessToken,
        'refreshToken': refreshToken,
        'expiresAt': expiresAt.toUtc().toIso8601String(),
        'user': user.toJson(),
      };

  bool isExpired({DateTime? now}) => !expiresAt.isAfter(now ?? DateTime.now());
}

/// The `user` object of the login response.
class AuthUserModel {
  final String id;
  final String email;
  final String fullName;
  final String? role;
  final bool isActive;

  /// Either bare names, or `{name, validUntil}` maps.
  final List<Object?> qualifications;
  final bool isOnLeave;
  final DateTime? leaveUntil;
  final String? managerId;

  const AuthUserModel({
    required this.id,
    required this.email,
    required this.fullName,
    this.role,
    this.isActive = false,
    this.qualifications = const [],
    this.isOnLeave = false,
    this.leaveUntil,
    this.managerId,
  });

  factory AuthUserModel.fromJson(Map<String, dynamic> json) {
    final leaveUntil = json['leaveUntil'] as String?;
    return AuthUserModel(
      id: json['id'] as String,
      email: json['email'] as String? ?? '',
      fullName: json['fullName'] as String? ?? '',
      role: json['role'] as String?,
      // A missing `isActive` means "not activated", not "active".
      isActive: json['isActive'] as bool? ?? false,
      qualifications: json['qualifications'] as List<dynamic>? ?? const [],
      isOnLeave: json['isOnLeave'] as bool? ?? false,
      leaveUntil: leaveUntil == null ? null : DateTime.tryParse(leaveUntil),
      managerId: json['managerId'] as String?,
    );
  }

  Map<String, dynamic> toJson() =>
      {
        'id': id,
        'email': email,
        'fullName': fullName,
        'role': role,
        'isActive': isActive,
        'qualifications': qualifications,
        'isOnLeave': isOnLeave,
        'leaveUntil': leaveUntil?.toUtc().toIso8601String(),
        'managerId': managerId,
      };

  /// Returns `null` when [role] isn't one of [UserRole]'s values, since a
  /// user without a valid role has no home screen.
  AppUser? toEntity() {
    final parsedRole = UserRole.fromValue(role);
    if (parsedRole == null) return null;
    return AppUser(
      id: id,
      name: fullName,
      email: email,
      role: parsedRole,
      qualifications: [
        for (final q in qualifications)
          if (_qualification(q) case final qualification?) qualification,
      ],
      active: isActive,
      onLeaveUntil: isOnLeave ? leaveUntil : null,
    );
  }

  static Qualification? _qualification(Object? json) {
    if (json is String) return Qualification(name: json);
    if (json is Map) {
      final validUntil = json['validUntil'];
      return Qualification(
        name: json['name'] as String? ?? '',
        validUntil: validUntil is String ? DateTime.tryParse(validUntil) : null,
      );
    }
    return null;
  }
}
