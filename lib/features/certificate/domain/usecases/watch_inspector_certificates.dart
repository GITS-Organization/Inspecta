import '../entities/certificate.dart';
import '../repositories/certificate_repository.dart';

class WatchInspectorCertificates {
  final CertificateRepository _repository;

  const WatchInspectorCertificates(this._repository);

  Stream<List<Certificate>> call(String inspectorId) => _repository.watchInspectorCertificates(inspectorId);
}
