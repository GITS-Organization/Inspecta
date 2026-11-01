import '../repositories/clients_repository.dart';

/// Removes a registered address or domain from a client.
class RemoveClientEmail {
  final ClientsRepository _repository;

  const RemoveClientEmail(this._repository);

  Future<void> call(String clientId, String ruleId) => _repository.removeEmail(clientId, ruleId);
}
