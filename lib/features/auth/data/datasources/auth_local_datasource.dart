import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../models/auth_session_model.dart';

/// Device-local auth state: the signed-in session (tokens and user) and
/// whether the user asked to stay signed in.
class AuthLocalDataSource {
  static const _keepSignedInKey = 'keep_signed_in';
  static const _sessionKey = 'auth_session';

  final FlutterSecureStorage _storage;

  /// The session as last read or written, so each API request doesn't
  /// re-read and re-parse secure storage just to find the access token.
  AuthSessionModel? _session;
  bool _loaded = false;

  AuthLocalDataSource(this._storage);

  Future<bool> keepSignedIn() async {
    return await _storage.read(key: _keepSignedInKey) != 'false';
  }

  Future<void> setKeepSignedIn(bool value) {
    return _storage.write(key: _keepSignedInKey, value: value.toString());
  }

  /// The stored session, or `null` when signed out. An unreadable record is
  /// discarded rather than thrown, so it can't lock the user out.
  Future<AuthSessionModel?> readSession() async {
    if (_loaded) return _session;
    final raw = await _storage.read(key: _sessionKey);
    if (raw != null) {
      try {
        _session = AuthSessionModel.fromJson(
          jsonDecode(raw) as Map<String, dynamic>,
        );
      } catch (e) {
        debugPrint('Discarding unreadable stored session: $e');
        await clearSession();
      }
    }
    _loaded = true;
    return _session;
  }

  Future<void> saveSession(AuthSessionModel session) async {
    await _storage.write(key: _sessionKey, value: jsonEncode(session.toJson()));
    _session = session;
    _loaded = true;
  }

  Future<void> clearSession() async {
    await _storage.delete(key: _sessionKey);
    _session = null;
    _loaded = true;
  }

  /// The bearer token for API requests, or `null` when signed out.
  Future<String?> accessToken() async => (await readSession())?.accessToken;
}
