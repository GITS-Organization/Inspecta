import '../entities/quote_email.dart';
import '../repositories/quotation_repository.dart';

/// The email that would carry a quotation's PDF to the client, ready to
/// edit. Sends nothing.
class GetQuoteEmail {
  final QuotationRepository _repository;

  const GetQuoteEmail(this._repository);

  Future<QuoteEmailPreview> call({
    required String requestId,
    required String quotationId,
    required bool accept,
  }) => _repository.getEmailPreview(requestId: requestId, quotationId: quotationId, accept: accept);
}
