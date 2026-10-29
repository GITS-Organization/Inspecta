import 'package:firebase_auth/firebase_auth.dart';

/// What auth still does through Firebase: the password-reset email, sent by
/// Firebase Auth until the API exposes its own reset flow. Login, refresh and
/// logout are in [AuthApiClientServices].
///
/// The Firebase instance is resolved lazily so that constructing this class
/// doesn't throw when it failed to initialise.
class AuthRemoteDataSource {
  final FirebaseAuth? _authOverride;

  AuthRemoteDataSource({FirebaseAuth? auth}) : _authOverride = auth;

  FirebaseAuth get _auth => _authOverride ?? FirebaseAuth.instance;

  Future<void> sendPasswordReset({
    required String email,
    required String languageCode,
  }) async {
    await _auth.setLanguageCode(languageCode);
    await _auth.sendPasswordResetEmail(email: email);
  }
}
