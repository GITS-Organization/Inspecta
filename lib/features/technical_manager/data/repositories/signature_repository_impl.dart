import '../../domain/repositories/signature_repository.dart';
import '../datasources/signature_remote_datasource.dart';

class SignatureRepositoryImpl implements SignatureRepository {
  final SignatureRemoteDataSource _remote;

  const SignatureRepositoryImpl(this._remote);

  @override
  Stream<List<List<double>>> watchSignature(String managerId) => _remote.watchSignature(managerId);

  @override
  Future<void> saveSignature(String managerId, List<List<double>> strokes) => _remote.save(managerId, strokes);
}
