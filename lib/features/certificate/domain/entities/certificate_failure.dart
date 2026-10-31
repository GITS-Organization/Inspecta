import '../../../../core/error/failure.dart';

enum CertificateFailureCode {
  permissionDenied,
  network,

  /// The API refused a submit because the certificate still has gaps.
  incomplete,
  unknown,
}

class CertificateFailure extends Failure {
  final CertificateFailureCode code;

  const CertificateFailure(this.code);

  @override
  String toString() => 'CertificateFailure(${code.name})';
}
