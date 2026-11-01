import '../entities/client.dart';

/// Methods throw [ClientsFailure] on expected errors.
abstract interface class ClientsRepository {
  /// The clients of the user's team, by name.
  Future<List<Client>> getClients();

  /// The machines registered for [clientId].
  Future<List<ClientMachine>> getMachines(String clientId);

  /// The addresses and domains registered for [clientId].
  Future<List<ClientEmail>> getEmails(String clientId);

  /// Registers [pattern] — an address, or a domain — for [clientId]. Every
  /// waiting "maybe" email from it becomes a request of the client; the
  /// answer's `promotedRequests` says how many.
  Future<ClientEmail> addEmail(String clientId, String pattern);

  /// Removes a registered address or domain.
  Future<void> removeEmail(String clientId, String ruleId);
}
