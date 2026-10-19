import '../repositories/signature_repository.dart';

class WatchSignature {
  final SignatureRepository _repository;

  const WatchSignature(this._repository);

  Stream<List<List<double>>> call(String managerId) => _repository.watchSignature(managerId);
}
