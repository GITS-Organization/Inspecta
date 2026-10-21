import '../entities/client.dart';

/// Methods emit / throw [ClientsFailure] on expected errors.
abstract interface class ClientsRepository {
  /// Every client, alphabetical by company name.
  Stream<List<Client>> watchClients();

  /// A single client, or `null` if it doesn't exist.
  Future<Client?> getById(String id);
}
