import '../../../../core/error/failure.dart';

enum RequestsFailureCode {
  permissionDenied,
  network,

  /// The request was changed by someone else since it was loaded.
  conflict,
  unknown,
}

class RequestsFailure extends Failure {
  final RequestsFailureCode code;

  const RequestsFailure(this.code);

  @override
  String toString() => 'RequestsFailure(${code.name})';
}
