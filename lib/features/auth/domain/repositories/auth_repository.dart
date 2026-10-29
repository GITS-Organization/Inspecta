import '../entities/user.dart';

/// Authentication contract. Methods throw [AuthFailure] on expected errors.
abstract interface class AuthRepository {
  Future<void> applySessionPolicy();

  Stream<AppUser?> watch();

  Future<AppUser?> getCurrentUser();

  Future<AppUser> signIn({
    required String email,
    required String password,
    required bool keepSignedIn,
  });

  /// Renews the stored session with its refresh token, without the user
  /// signing in again. Returns false when there is no session to renew or the
  /// API refuses it. Concurrent calls share one request.
  Future<bool> refreshSession();

  Future<void> signOut();

  Future<void> sendPasswordReset({
    required String email,
    required String languageCode,
  });
}
