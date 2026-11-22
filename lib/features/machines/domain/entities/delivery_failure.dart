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

  /// 409 `email_too_large`: with the ticked reports the email is over the size
  /// limit. Nothing was sent or changed; unticking some reports fixes it.
  emailTooLarge,

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
