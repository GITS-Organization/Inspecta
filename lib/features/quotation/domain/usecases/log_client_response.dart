import '../entities/client_response_outcome.dart';
import '../repositories/quotation_repository.dart';

/// BR-03.6/03.7: logs how the client answered a sent quotation. An
/// `accepted` outcome also supersedes every other quotation on the request
/// (BR-03.7: the agreed price is locked to this one version).
///
/// Not available yet: the API has no endpoint for it, so this always throws
/// a `QuotationFailure` (see [QuotationRepository.recordClientResponse]).
class LogClientResponse {
  final QuotationRepository _quotations;

  const LogClientResponse(this._quotations);

  Future<void> call({
    required String quotationId,
    required String requestId,
    required ClientResponseOutcome outcome,
    int? clientPricePiastres,
    String? note,
  }) {
    return _quotations.recordClientResponse(
      quotationId: quotationId,
      requestId: requestId,
      outcome: outcome,
      clientPricePiastres: clientPricePiastres,
      note: note?.trim(),
    );
  }
}
