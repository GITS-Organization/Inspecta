import '../entities/client.dart';
import '../repositories/clients_repository.dart';

/// The clients of the user's team, by name.
class GetClients {
  final ClientsRepository _repository;

  const GetClients(this._repository);

  Future<List<Client>> call() => _repository.getClients();
}
