import '../../../../core/enums/job_status.dart';
import '../../../requests/domain/repositories/requests_repository.dart';
import '../entities/quote_line_item.dart';
import '../entities/reply_type.dart';
import '../repositories/quotation_repository.dart';

/// BR-03.5/03.8: sends the quotation (creates its version) and moves the
/// request on — `quote_sent` for accept/offer, `quote_rejected` for
/// reject. Every reply type is recorded here so the Quotations tracker
/// has a complete history (the earlier "reject bypasses the quotations
/// collection" design didn't fit that tracker).
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

    if (type == ReplyType.reject) {
      await _requests.rejectRequest(requestId: requestId, reason: message ?? '');
    } else {
      await _requests.updateStatus(
        requestId,
        JobStatus.quoteSent,
        note: 'Quotation v$version sent',
      );
    }
    return version;
  }
}
