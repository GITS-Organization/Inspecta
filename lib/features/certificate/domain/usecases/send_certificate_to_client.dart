import '../repositories/certificate_repository.dart';

/// The coordinator sends a certificate the technical manager approved on to
/// the client.
class SendCertificateToClient {
  final CertificateRepository _repository;

  const SendCertificateToClient(this._repository);

  Future<void> call(String requestId, {required String email}) =>
      _repository.markSentToClient(requestId, email: email);
}
