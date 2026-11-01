import '../entities/client.dart';
import '../repositories/clients_repository.dart';

/// The addresses and domains registered for a client.
class GetClientEmails {
  final ClientsRepository _repository;

  const GetClientEmails(this._repository);

  Future<List<ClientEmail>> call(String clientId) => _repository.getEmails(clientId);
}
