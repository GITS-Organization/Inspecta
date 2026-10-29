import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/base/result.dart';
import 'package:inspecta/core/base/safe_request_handler.dart';
import 'package:inspecta/core/error/api_failure.dart';
import 'package:inspecta/core/error/failure.dart';

class _MyFailure extends Failure {
  const _MyFailure();
}

DioException _badResponse(int status, [Object? data]) {
  final options = RequestOptions(path: 'x');
  return DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response(requestOptions: options, statusCode: status, data: data),
  );
}

void main() {
  const safeApi = SafeApi();

  test('a request that completes is a Success with its data', () async {
    final result = await safeApi(() async => 42);

    expect(result, isA<Success<int>>());
    expect(result.fold((data) => data, (_) => -1), 42);
  });

  test('a void request is a Success too', () async {
    final result = await safeApi<void>(() async {});

    expect(result, isA<Success<void>>());
  });

  test('a server error becomes an ApiFailure with its status and message', () async {
    final result = await safeApi<int>(
      () async => throw _badResponse(403, {'message': 'ACCOUNT_DISABLED'}),
    );

    final failure = (result as Err<int>).failure as ApiFailure;
    expect(failure.code, 403);
    expect(failure.error.message, 'Account Disabled');
  });

  test('a connection error is an ApiFailure without a status', () async {
    final result = await safeApi<int>(
      () async => throw DioException(
        requestOptions: RequestOptions(path: 'x'),
        type: DioExceptionType.connectionError,
      ),
    );

    final failure = (result as Err<int>).failure as ApiFailure;
    expect(failure.code, isNull);
    expect(failure.error.message, 'Connection to server failed');
  });

  test('an error that is not a Dio one is still an ApiFailure, not a throw', () async {
    final result = await safeApi<int>(() async => throw const FormatException('bad'));

    expect((result as Err<int>).failure, isA<ApiFailure>());
  });

  test('mapError picks the failure for errors it knows', () async {
    final result = await safeApi<int>(
      () async => throw _badResponse(401),
      mapError: (error) => const _MyFailure(),
    );

    expect((result as Err<int>).failure, isA<_MyFailure>());
  });

  test('mapError returning null falls back to the generic failure', () async {
    final result = await safeApi<int>(
      () async => throw _badResponse(500),
      mapError: (error) => null,
    );

    final failure = (result as Err<int>).failure as ApiFailure;
    expect(failure.code, 500);
  });
}
