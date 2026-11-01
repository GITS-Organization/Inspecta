import '../../../../core/error/failure.dart';

enum DeliveryFailureCode {
  permissionDenied,
  network,

  /// 400: an address (To or Cc) isn't valid.
  invalidAddress,

  /// 409 `no_recipient`: there is no address to send to.
  noRecipient,

  /// 502: the mail server refused the email; nothing was sent or changed, so
  /// sending again is safe.
  mailRefused,

  /// The job changed under the coordinator (already sent, no longer ready).
  conflict,
  unknown,
}

class DeliveryFailure extends Failure {
  final DeliveryFailureCode code;

  const DeliveryFailure(this.code);

  @override
  String toString() => 'DeliveryFailure(${code.name})';
}
