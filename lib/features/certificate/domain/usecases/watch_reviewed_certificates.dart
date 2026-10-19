import '../entities/certificate.dart';
import '../repositories/certificate_repository.dart';

class WatchReviewedCertificates {
  final CertificateRepository _repository;

  const WatchReviewedCertificates(this._repository);

  Stream<List<Certificate>> call(DateTime since) => _repository.watchReviewedSince(since);
}
