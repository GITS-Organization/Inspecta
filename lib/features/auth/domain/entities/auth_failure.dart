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

  /// The company's subscription has ended (403 `subscription_inactive`).
  subscriptionInactive,
  network,

  /// The API has no endpoint for this yet: the password-reset request.
  unavailable,
  unknown,
}

class AuthFailure extends Failure {
  final AuthFailureCode code;

  const AuthFailure(this.code);

  @override
  String toString() => 'AuthFailure(${code.name})';
}
