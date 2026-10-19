import '../repositories/signature_repository.dart';

class SaveSignature {
  final SignatureRepository _repository;

  const SaveSignature(this._repository);

  Future<void> call(String managerId, List<List<double>> strokes) => _repository.saveSignature(managerId, strokes);
}
