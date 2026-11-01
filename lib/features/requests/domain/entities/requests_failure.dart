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

/// Confirming a "maybe" email was refused (409 `client_required`): its sender
/// isn't registered under any client yet. Registering the sender's address or
/// domain on a client moves the email on by itself.
class ClientRequiredFailure extends RequestsFailure {
  const ClientRequiredFailure() : super(RequestsFailureCode.conflict);

  @override
  String toString() => 'ClientRequiredFailure()';
}

/// Saving a request's items was refused (400): a selected machine belongs to
/// another client, or doesn't exist. Nothing was saved.
class InvalidMachineFailure extends RequestsFailure {
  const InvalidMachineFailure() : super(RequestsFailureCode.unknown);

  @override
  String toString() => 'InvalidMachineFailure()';
}
