import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:pretty_dio_logger/pretty_dio_logger.dart';

import '../../features/auth/data/datasources/auth_local_datasource.dart';
import '../../features/auth/domain/usecases/refresh_session.dart';
import '../../features/auth/domain/usecases/sign_out.dart';
import '../../injection.dart';
import '../consts/constants.dart';
import '../framework/mtoast.dart';
import 'api_urls.dart';

class DioFactory {
  DioFactory._();

  static Dio? dio;
  static bool isAuthenticating = false;

  static Dio getDio() {
    if (dio == null) {
      dio = Dio();

      dio!
        ..options.baseUrl = Constants.baseUrl + Constants.apiPrefix
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

  /// Marks a request that was already re-sent with a renewed token, so a
  /// second 401 signs out instead of looping.
  static const _retriedKey = 'retriedAfterRefresh';

  /// A 401 from login means wrong credentials, from logout that the token is
  /// already dead, and from refresh that the refresh token was refused: none
  /// is an expired access token to renew.
  static bool _isSessionExpired(DioException error) =>
      error.response?.statusCode == 401 &&
      error.requestOptions.path != ApiUrls.login &&
      error.requestOptions.path != ApiUrls.logout &&
      error.requestOptions.path != ApiUrls.refresh;

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
        // The signed-in user's access token, read per request so a new
        // sign-in is picked up. A request that sets its own is left alone.
        onRequest: (RequestOptions options, handler) async {
          if (!options.headers.containsKey('Authorization')) {
            final token = await getIt<AuthLocalDataSource>().accessToken();
            if (token != null) {
              options.headers['Authorization'] = 'Bearer $token';
            }
          }
          return handler.next(options);
        },
        // The API can label a JSON body `text/plain`, which Dio then leaves
        // as a string.
        onResponse: (Response response, handler) {
          final data = response.data;
          if (data is String && data.isNotEmpty) {
            try {
              response.data = jsonDecode(data);
            } on FormatException {
              // Not JSON: leave the body as it is.
            }
          }
          return handler.next(response);
        },
        onError: (DioException error, handler) async {
          if (!_isSessionExpired(error) || DioFactory.isAuthenticating) {
            return handler.next(error);
          }
          // The access token ran out: renew it and send the request again.
          final options = error.requestOptions;
          if (options.extra[_retriedKey] == true) {
            // Refused even with a renewed token: this endpoint denies the
            // user, which says nothing about whether the session is alive.
            return handler.next(error);
          }
          if (await getIt<RefreshSession>()()) {
            options.extra[_retriedKey] = true;
            options.headers.remove('Authorization'); // re-attached with the new token
            try {
              return handler.resolve(await getDio().fetch<dynamic>(options));
            } on DioException catch (retryError) {
              return handler.next(retryError);
            }
          }
          debugPrint('Signed out: 401 on ${options.path} and the refresh failed');
          MToast.showError(
            message: 'Session expired. Please sign in to continue.',
          );
          await getIt<SignOut>()();
          return handler.next(error);
        },
      ),
    );
  }
}
