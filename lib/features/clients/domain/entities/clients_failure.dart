import '../../../../core/error/failure.dart';

enum ClientsFailureCode {
  permissionDenied,
  network,

  /// 400: not a valid email address or domain.
  invalidPattern,

  /// 404: the client isn't there (or isn't on the user's team).
  notFound,

  /// 409: this address or domain is already registered.
  duplicate,
  unknown,
}

class ClientsFailure extends Failure {
  final ClientsFailureCode code;

  const ClientsFailure(this.code);

  @override
  String toString() => 'ClientsFailure(${code.name})';
}
