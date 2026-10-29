import '../../../requests/domain/repositories/requests_repository.dart';
import '../entities/quote_line_item.dart';
import '../entities/reply_type.dart';
import '../repositories/quotation_repository.dart';

/// BR-03.5/03.8: sends the quotation (creates its version). An accept or
/// offer goes to the API (`POST /requests/{id}/quotes`), which moves the
/// request to `quote_sent`; a reject is recorded and the request closed as
/// `quote_rejected`.
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
    int? validityDays,
    String? message,
    List<String>? notes,
    List<String>? scopeOfWork,
  }) async {
    final version = await _quotations.send(
      requestId: requestId,
      requestNumber: requestNumber,
      clientName: clientName,
      equipmentSummary: equipmentSummary,
      type: type,
      items: items,
      validityDays: validityDays,
      message: message?.trim(),
      notes: notes,
      scopeOfWork: scopeOfWork,
    );

    // A rejection still closes the request here. A sent quote doesn't touch
    // the request: the API moves it to `quote_sent` itself.
    if (type == ReplyType.reject) {
      await _requests.rejectRequest(requestId: requestId, reason: message ?? '');
    }
    return version;
  }
}
