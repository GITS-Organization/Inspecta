import '../../../../core/utils/currency.dart';
import '../../../requests/domain/repositories/requests_repository.dart';
import '../entities/quote_email.dart';
import '../entities/reply_type.dart';
import '../repositories/quotation_repository.dart';

/// BR-03.5/03.8: sends the reply. An offer is sent, and an accept accepted,
/// on the API's quote (`.../quotes/{id}/send`, `.../accept`), which moves the
/// request on itself; a reject goes to `POST /requests/{id}/reject`.
///
/// The API emails the client the PDF as part of sending or accepting; the
/// coordinator can edit that email first (see [QuoteEmailEdits]).
class SendQuotation {
  final QuotationRepository _quotations;
  final RequestsRepository _requests;

  const SendQuotation(this._quotations, this._requests);

  Future<int> call({
    required String requestId,
    required ReplyType type,
    String? quotationId,
    String currency = Currency.egp,
    int? validityDays,
    String? message,
    QuoteEmailEdits? email,
  }) async {
    // A rejection goes to the API on its own: it closes the request and
    // tells the client why, with no quote recorded.
    if (type == ReplyType.reject) {
      await _requests.rejectRequest(requestId: requestId, reason: message?.trim() ?? '');
      return 0;
    }

    return _quotations.send(
      requestId: requestId,
      type: type,
      quotationId: quotationId,
      currency: currency,
      validityDays: validityDays,
      email: email,
    );
  }
}
