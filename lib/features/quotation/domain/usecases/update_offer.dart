import '../../../../core/utils/currency.dart';
import '../entities/quote_line_item.dart';
import '../repositories/quotation_repository.dart';

/// Saves a priced offer onto its quote through the API — what Save draft and
/// Send do for a "Send price offer" reply, and what the PDF preview reads.
class UpdateOffer {
  final QuotationRepository _repository;

  const UpdateOffer(this._repository);

  Future<void> call({
    required String requestId,
    required String quotationId,
    required List<QuoteLineItem> items,
    String currency = Currency.egp,
    int? validityDays,
    List<String>? scopeOfWork,
    List<String>? notes,
    String? message,
  }) {
    return _repository.updateOffer(
      requestId: requestId,
      quotationId: quotationId,
      items: items,
      currency: currency,
      validityDays: validityDays,
      scopeOfWork: scopeOfWork,
      notes: notes,
      message: message?.trim(),
    );
  }
}
