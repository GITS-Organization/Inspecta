import '../entities/quotation.dart';
import '../repositories/quotation_repository.dart';

/// Ignores a client's reply that needs no action ("thanks, we'll look into
/// it"): the reply is cleared and the request waits for the client again.
class DismissClientReply {
  final QuotationRepository _repository;

  const DismissClientReply(this._repository);

  Future<Quotation> call({required String requestId, required String quotationId}) =>
      _repository.dismissReply(requestId: requestId, quotationId: quotationId);
}
