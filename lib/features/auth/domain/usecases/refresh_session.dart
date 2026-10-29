import '../repositories/auth_repository.dart';

class RefreshSession {
  final AuthRepository _repository;

  const RefreshSession(this._repository);

  Future<bool> call() => _repository.refreshSession();
}
