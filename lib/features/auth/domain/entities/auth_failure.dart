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

  /// Change password: the current password typed was wrong.
  currentPasswordIncorrect,

  /// Change password: the new password breaks the API's password rules.
  weakPassword,

  /// Change password: the new password is the same as the current one.
  passwordUnchanged,
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
