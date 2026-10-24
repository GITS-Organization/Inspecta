import 'package:dio/dio.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/foundation.dart';
import 'package:pretty_dio_logger/pretty_dio_logger.dart';

import '../../features/auth/domain/usecases/sign_out.dart';
import '../../injection.dart';
import '../framework/mtoast.dart';

class DioFactory {
  DioFactory._();

  static Dio? dio;
  static bool isAuthenticating = false;

  static Dio getDio() {
    if (dio == null) {
      dio = Dio();

      dio!
        ..options.connectTimeout = const Duration(seconds: 30)
        ..options.receiveTimeout = const Duration(seconds: 60)
        ..options.sendTimeout = const Duration(seconds: 30);
      addDioHeaders();
      addDioInterceptor();
      return dio!;
    } else {
      return dio!;
    }
  }

  static void addDioHeaders() {
    dio?.options.headers = {'Accept': 'application/json', 'accept': '*/*'};
  }

  static bool _isSessionExpired(DioException error) =>
      error.response?.statusCode == 401;

  static void addDioInterceptor() {
    dio?.interceptors.add(
      PrettyDioLogger(
        requestBody: kDebugMode,
        requestHeader: kDebugMode,
        responseBody: kDebugMode,
        responseHeader: kDebugMode,
      ),
    );
    dio?.interceptors.add(
      InterceptorsWrapper(
        // The signed-in user's Firebase ID token, refreshed by Firebase when
        // it is close to expiring, so it is read per request.
        onRequest: (RequestOptions options, handler) async {
          final token = await FirebaseAuth.instance.currentUser?.getIdToken();
          if (token != null) options.headers['Authorization'] = 'Bearer $token';
          return handler.next(options);
        },
        onError: (DioException error, handler) async {
          if (_isSessionExpired(error) && !DioFactory.isAuthenticating) {
            MToast.showError(
              message: 'Session expired. Please sign in to continue.',
            );
            await getIt<SignOut>()();
          }
          return handler.next(error);
        },
      ),
    );
  }
}
