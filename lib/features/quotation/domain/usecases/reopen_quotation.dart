import '../entities/quotation.dart';
import '../repositories/quotation_repository.dart';

/// Reopens a sent quote as a new revision with the same prices, to change it
/// after a phone call or the client's reply. The request goes back to a
/// draft quote until the revision is sent.
class ReopenQuotation {
  final QuotationRepository _repository;

  const ReopenQuotation(this._repository);

  Future<Quotation> call({required String requestId, required String quotationId}) =>
      _repository.reopen(requestId: requestId, quotationId: quotationId);
}
