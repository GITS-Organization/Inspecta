import 'package:dio/dio.dart';
import 'package:firebase_auth/firebase_auth.dart';

import '../../../../core/base/result.dart';
import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/networking/api_urls.dart';
import '../../../../core/networking/dio_factory.dart';
import '../models/auth_session_model.dart';

/// Login and logout go to the Inspecta API. The password-reset email is
/// still sent by Firebase Auth until the API exposes its own reset flow.
///
/// The Dio and Firebase instances are resolved lazily so that constructing
/// this class doesn't throw when either failed to initialise.
class AuthRemoteDataSource {
  static const _safeApi = SafeApi();

  final Dio? _dioOverride;
  final FirebaseAuth? _authOverride;

  AuthRemoteDataSource({Dio? dio, FirebaseAuth? auth})
    : _dioOverride = dio,
      _authOverride = auth;

  Dio get _dio => _dioOverride ?? DioFactory.getDio();

  FirebaseAuth get _auth => _authOverride ?? FirebaseAuth.instance;

  Future<Result<AuthSessionModel>> login({
    required String email,
    required String password,
  }) {
    return _safeApi(() async {
      final response = await _dio.post<Map<String, dynamic>>(
        ApiUrls.login,
        data: {'email': email, 'password': password},
      );
      return AuthSessionModel.fromJson(response.data!);
    });
  }

  /// Trades [refreshToken] for a new session (new tokens, same user).
  Future<Result<AuthSessionModel>> refresh({required String refreshToken}) {
    return _safeApi(() async {
      final response = await _dio.post<Map<String, dynamic>>(
        ApiUrls.refresh,
        data: {'refreshToken': refreshToken},
      );
      return AuthSessionModel.fromJson(response.data!);
    });
  }

  /// Revokes [refreshToken] server-side. [accessToken] is passed explicitly
  /// because the stored session may already be gone when this is called.
  Future<Result<void>> logout({
    required String accessToken,
    required String refreshToken,
  }) {
    return _safeApi<void>(() async {
      await _dio.post<void>(
        ApiUrls.logout,
        data: {'refreshToken': refreshToken},
        options: Options(headers: {'Authorization': 'Bearer $accessToken'}),
      );
    });
  }

  Future<void> sendPasswordReset({
    required String email,
    required String languageCode,
  }) async {
    await _auth.setLanguageCode(languageCode);
    await _auth.sendPasswordResetEmail(email: email);
  }
}
