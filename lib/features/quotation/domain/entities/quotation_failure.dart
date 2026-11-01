import '../../../../core/error/failure.dart';

enum QuotationFailureCode {
  permissionDenied,
  network,

  /// 400 on sending: an address (To or Cc) isn't valid; nothing was sent.
  invalidAddress,

  /// 502 on sending: the mail server refused the email; nothing changed, so
  /// sending again is safe.
  mailRefused,
  unknown,
}

class QuotationFailure extends Failure {
  final QuotationFailureCode code;

  const QuotationFailure(this.code);

  @override
  String toString() => 'QuotationFailure(${code.name})';
}
