import 'api_error_model.dart';
import 'failure.dart';

/// A request that failed for a reason no feature gave its own [Failure] to.
/// Wraps what [ApiErrorHandler] made of the error — a readable message, the
/// HTTP status when there was one — so a repository can still map it.
class ApiFailure extends Failure {
  final BaseErrorModel error;

  ApiFailure(this.error);

  /// The HTTP status code, when the server answered.
  int? get code => error.code;

  @override
  String toString() => 'ApiFailure($error)';
}
