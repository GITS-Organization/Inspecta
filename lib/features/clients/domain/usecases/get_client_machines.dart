import '../entities/client.dart';
import '../repositories/clients_repository.dart';

/// The machines registered for a client.
class GetClientMachines {
  final ClientsRepository _repository;

  const GetClientMachines(this._repository);

  Future<List<ClientMachine>> call(String clientId) => _repository.getMachines(clientId);
}
