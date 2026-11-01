import '../entities/client.dart';
import '../repositories/clients_repository.dart';

/// Registers an address or domain for a client: its waiting "maybe" emails
/// become the client's requests.
class AddClientEmail {
  final ClientsRepository _repository;

  const AddClientEmail(this._repository);

  Future<ClientEmail> call(String clientId, String pattern) => _repository.addEmail(clientId, pattern.trim());
}
