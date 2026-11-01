import '../entities/machine_suggestion.dart';
import '../repositories/requests_repository.dart';

/// The client's machines a request's email seems to ask about.
class GetMachineSuggestions {
  final RequestsRepository _repository;

  const GetMachineSuggestions(this._repository);

  Future<List<MachineSuggestion>> call(String requestId) => _repository.getSuggestions(requestId);
}
