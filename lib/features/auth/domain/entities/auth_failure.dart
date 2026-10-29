import '../../../../core/error/failure.dart';

enum AuthFailureCode {
  invalidEmail,
  invalidCredentials,

  /// The user is inactive (BR-01.4), or the API has disabled the account.
  accountDisabled,

  /// Signed in, but the user has no valid role, so we can't pick a home
  /// screen.
  noProfile,
  tooManyAttempts,
  network,

  /// Firebase isn't initialised (e.g. `flutterfire configure` not run yet),
  /// which only the password-reset email depends on.
  unavailable,
  unknown,
}

class AuthFailure extends Failure {
  final AuthFailureCode code;

  const AuthFailure(this.code);

  @override
  String toString() => 'AuthFailure(${code.name})';
}
