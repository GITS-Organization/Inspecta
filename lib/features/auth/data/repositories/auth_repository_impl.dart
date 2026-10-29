import 'dart:async';

import 'package:dio/dio.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/error/api_error_model.dart';
import '../../../../core/error/api_failure.dart';
import '../../domain/entities/auth_failure.dart';
import '../../domain/entities/user.dart';
import '../../domain/repositories/auth_repository.dart';
import '../datasources/auth_api_client_services.dart';
import '../datasources/auth_local_datasource.dart';
import '../datasources/auth_remote_datasource.dart';
import '../models/auth_requests.dart';
import '../models/auth_session_model.dart';

class AuthRepositoryImpl implements AuthRepository {
  static const _safeApi = SafeApi();

  final AuthApiClientServices _api;
  final AuthRemoteDataSource _remote;
  final AuthLocalDataSource _local;

  /// Session changes after the first [watch] event: sign-in and sign-out.
  final _changes = StreamController<AppUser?>.broadcast();

  AuthRepositoryImpl(this._api, this._remote, this._local);

  @override
  Future<void> applySessionPolicy() async {
    try {
      final session = await _local.readSession();
      if (session != null && !await _local.keepSignedIn()) {
        await _local.clearSession();
        unawaited(_revoke(session));
      }
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Stream<AppUser?> watch() async* {
    try {
      yield await getCurrentUser();
    } on AuthFailure {
      // Unreadable storage: start signed out rather than stuck on a splash.
      yield null;
    }
    yield* _changes.stream;
  }

  @override
  Future<AppUser?> getCurrentUser() async {
    try {
      var session = await _local.readSession();
      if (session == null) return null;
      if (session.isExpired()) {
        // A remembered session outlives its access token: renew it quietly.
        switch (await _renewSession()) {
          case _Renewal.renewed:
            // The API just issued this session, so it is trusted as it
            // stands: the device's clock isn't asked again.
            session = await _local.readSession();
          case _Renewal.unreachable:
            // No answer (offline at startup, server down): keep the session.
            // Its token is renewed when the first request needs it.
            debugPrint('Session kept: refresh could not reach the API');
          case _Renewal.rejected:
            debugPrint('Session dropped: the API refused the refresh token');
            await _local.clearSession();
            return null;
        }
      }
      if (session == null || _validate(session.user) != null) {
        debugPrint('Session dropped: the stored user is not allowed in');
        await _local.clearSession();
        return null;
      }
      return session.user.toEntity();
    } catch (e) {
      throw _mapError(e);
    }
  }

  Future<_Renewal>? _renewing;

  @override
  Future<bool> refreshSession() async => await _renewSession() == _Renewal.renewed;

  /// One renewal at a time: callers that arrive while it runs share it.
  Future<_Renewal> _renewSession() {
    return _renewing ??= _renew().whenComplete(() => _renewing = null);
  }

  Future<_Renewal> _renew() async {
    try {
      final session = await _local.readSession();
      if (session == null) return _Renewal.rejected;
      final result = await _safeApi(
        () => _api.refresh(RefreshTokenRequest(refreshToken: session.refreshToken)),
      );
      Object? failure;
      final fresh = result.fold<AuthSessionModel?>((data) => data, (f) {
        debugPrint('Session refresh failed: $f');
        failure = f;
        return null;
      });
      if (fresh == null) {
        // A 4xx means the API understood and said no; anything else (no
        // connection, a 5xx) says nothing about the refresh token.
        final status = failure is ApiFailure ? (failure as ApiFailure).code : null;
        return status != null && status >= 400 && status < 500
            ? _Renewal.rejected
            : _Renewal.unreachable;
      }
      if (_validate(fresh.user) != null) return _Renewal.rejected;
      await _local.saveSession(fresh);
      return _Renewal.renewed;
    } catch (e) {
      debugPrint('Session refresh failed: $e');
      return _Renewal.unreachable;
    }
  }

  @override
  Future<AppUser> signIn({
    required String email,
    required String password,
    required bool keepSignedIn,
  }) async {
    try {
      final result = await _safeApi(
        () => _api.login(LoginRequest(email: email, password: password)),
      );
      final session = result.fold((data) => data, (f) => throw _mapError(f));
      final failure = _validate(session.user);
      if (failure != null) {
        // The API has already issued tokens for this login.
        unawaited(_revoke(session));
        throw failure;
      }
      await _local.saveSession(session);
      await _local.setKeepSignedIn(keepSignedIn);
      final user = session.user.toEntity()!;
      _changes.add(user);
      return user;
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> signOut() async {
    try {
      final session = await _local.readSession();
      // Clear locally first: the user is signed out even if the network call
      // below fails.
      await _local.clearSession();
      _changes.add(null);
      if (session != null) await _revoke(session);
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> sendPasswordReset({
    required String email,
    required String languageCode,
  }) async {
    try {
      await _remote.sendPasswordReset(email: email, languageCode: languageCode);
    } on FirebaseAuthException catch (e) {
      // Don't reveal whether an account exists for this email.
      if (e.code == 'user-not-found') return;
      throw _mapError(e);
    } catch (e) {
      // FirebaseAuth.instance throws a non-Firebase error when no app exists.
      if (Firebase.apps.isEmpty) {
        throw const AuthFailure(AuthFailureCode.unavailable);
      }
      throw _mapError(e);
    }
  }

  /// Best-effort server-side logout; a failure only means the refresh token
  /// stays valid until it expires.
  Future<void> _revoke(AuthSessionModel session) async {
    final result = await _safeApi<void>(
      () => _api.logout(
        'Bearer ${session.accessToken}',
        RefreshTokenRequest(refreshToken: session.refreshToken),
      ),
    );
    result.fold((_) {}, (f) => debugPrint('Logout request failed: $f'));
  }

  /// Returns why [user] can't be signed in, or `null` if it can.
  AuthFailure? _validate(AuthUserModel user) {
    if (user.toEntity() == null) {
      return const AuthFailure(AuthFailureCode.noProfile);
    }
    if (!user.isActive) {
      return const AuthFailure(AuthFailureCode.accountDisabled);
    }
    return null;
  }

  AuthFailure _mapError(Object error) {
    if (error is AuthFailure) return error;
    if (error is ApiFailure) error = error.error;
    if (error is ApiErrorModel) {
      final status = error.code;
      if (status == null) {
        return AuthFailure(switch (error.dioException?.type) {
          DioExceptionType.connectionError ||
          DioExceptionType.connectionTimeout ||
          DioExceptionType.sendTimeout ||
          DioExceptionType.receiveTimeout ||
          DioExceptionType.unknown => AuthFailureCode.network,
          _ => AuthFailureCode.unknown,
        });
      }
      return AuthFailure(switch (status) {
        400 || 401 => AuthFailureCode.invalidCredentials,
        403 => AuthFailureCode.accountDisabled,
        429 => AuthFailureCode.tooManyAttempts,
        _ => AuthFailureCode.unknown,
      });
    }
    if (error is FirebaseAuthException) {
      return AuthFailure(switch (error.code) {
        'invalid-email' => AuthFailureCode.invalidEmail,
        'too-many-requests' => AuthFailureCode.tooManyAttempts,
        'network-request-failed' => AuthFailureCode.network,
        _ => AuthFailureCode.unknown,
      });
    }
    debugPrint('Unexpected auth error: $error');
    return const AuthFailure(AuthFailureCode.unknown);
  }
}

/// How a session renewal ended.
enum _Renewal {
  /// New tokens were issued and stored.
  renewed,

  /// The API refused the refresh token: the session is over.
  rejected,

  /// There was no usable answer, so the session may still be good.
  unreachable,
}
