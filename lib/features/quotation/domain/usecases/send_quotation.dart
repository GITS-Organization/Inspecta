import '../../../requests/domain/repositories/requests_repository.dart';
import '../entities/quote_line_item.dart';
import '../entities/reply_type.dart';
import '../repositories/quotation_repository.dart';

/// BR-03.5/03.8: sends the reply. An offer is sent, and an accept accepted,
/// on the API's quote (`.../quotes/{id}/send`, `.../accept`), which moves the
/// request on itself; a reject goes to `POST /requests/{id}/reject`.
///
/// Emailing the client their PDF happens outside the app (a backend job
/// this client can't do) — see the cubit's caller for how that gap is
/// surfaced.
class SendQuotation {
  final QuotationRepository _quotations;
  final RequestsRepository _requests;

  const SendQuotation(this._quotations, this._requests);

  Future<int> call({
    required String requestId,
    required String requestNumber,
    required String clientName,
    required String equipmentSummary,
    required ReplyType type,
    required List<QuoteLineItem> items,
    String? quotationId,
    int? validityDays,
    String? message,
    List<String>? notes,
    List<String>? scopeOfWork,
  }) async {
    // A rejection goes to the API on its own: it closes the request and
    // tells the client why, with no quote recorded.
    if (type == ReplyType.reject) {
      await _requests.rejectRequest(requestId: requestId, reason: message?.trim() ?? '');
      return 0;
    }

    final version = await _quotations.send(
      requestId: requestId,
      requestNumber: requestNumber,
      clientName: clientName,
      equipmentSummary: equipmentSummary,
      type: type,
      items: items,
      quotationId: quotationId,
      validityDays: validityDays,
      message: message?.trim(),
      notes: notes,
      scopeOfWork: scopeOfWork,
    );

    return version;
  }
}
