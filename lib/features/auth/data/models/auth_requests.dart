/// The body of `auth/login`.
class LoginRequest {
  final String email;
  final String password;

  const LoginRequest({required this.email, required this.password});

  Map<String, dynamic> toJson() => {'email': email, 'password': password};
}

/// The body of `auth/refresh` and `auth/logout`.
class RefreshTokenRequest {
  final String refreshToken;

  const RefreshTokenRequest({required this.refreshToken});

  Map<String, dynamic> toJson() => {'refreshToken': refreshToken};
}

/// The body of `auth/change-my-password`.
class ChangePasswordRequest {
  final String currentPassword;
  final String newPassword;

  /// This device's refresh token: the API keeps this device signed in and
  /// signs every other device out.
  final String? refreshToken;

  const ChangePasswordRequest({
    required this.currentPassword,
    required this.newPassword,
    this.refreshToken,
  });

  Map<String, dynamic> toJson() => {
    'currentPassword': currentPassword,
    'newPassword': newPassword,
    if (refreshToken != null) 'refreshToken': refreshToken,
  };
}
