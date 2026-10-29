import 'package:flutter/foundation.dart';

import '../error/api_error_handler.dart';
import '../error/api_failure.dart';
import '../error/failure.dart';
import 'result.dart';

/// Runs a request and turns anything it throws into an [Err], so callers read a
/// [Result] instead of wrapping every call in try/catch.
class SafeApi {
  const SafeApi();

  /// [mapError] picks the [Failure] for errors it knows (return null for the
  /// rest, which become an [ApiFailure] built by [ApiErrorHandler]).
  Future<Result<T>> call<T>(
    Future<T> Function() request, {
    Failure? Function(Object error)? mapError,
  }) async {
    try {
      return Success(await request());
    } catch (error, stack) {
      final mapped = mapError?.call(error);
      if (mapped != null) {
        // ApiFailure's error logs itself; a feature's own failure doesn't.
        debugPrint('$error\n$stack');
        return Err(mapped);
      }
      return Err(ApiFailure(ApiErrorHandler.handle(error, trace: stack)));
    }
  }
}
