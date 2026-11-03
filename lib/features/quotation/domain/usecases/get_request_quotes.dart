import '../entities/quotation.dart';
import '../repositories/quotation_repository.dart';

/// Every quote of a request, newest first, with the client's replies to the
/// current one.
class GetRequestQuotes {
  final QuotationRepository _repository;

  const GetRequestQuotes(this._repository);

  Future<List<Quotation>> call(String requestId) => _repository.listForRequest(requestId);
}
