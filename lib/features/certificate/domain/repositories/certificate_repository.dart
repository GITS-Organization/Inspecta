import '../entities/certificate.dart';

/// Methods emit / throw [CertificateFailure] on expected errors.
abstract interface class CertificateRepository {
  /// The one certificate for this job, or `null` until one exists.
  Stream<Certificate?> watchCertificate(String requestId);

  /// The coordinator sends an approved certificate on: records who it went to
  /// and moves the job to [JobStatus.sentToClient]. The client email itself is
  /// not sent from the app.
  Future<void> markSentToClient(String requestId, {required String email});
}
