import '../entities/quotation.dart';
import '../entities/quote_line_item.dart';
import '../entities/reply_type.dart';
import '../repositories/quotation_repository.dart';

class SaveDraft {
  final QuotationRepository _repository;

  const SaveDraft(this._repository);

  Future<Quotation> call({
    required String requestId,
    required String requestNumber,
    required String clientName,
    required String equipmentSummary,
    required ReplyType type,
    required List<QuoteLineItem> items,
    int? validityDays,
    String? message,
    List<String>? notes,
  }) {
    return _repository.saveDraft(
      requestId: requestId,
      requestNumber: requestNumber,
      clientName: clientName,
      equipmentSummary: equipmentSummary,
      type: type,
      items: items,
      validityDays: validityDays,
      message: message?.trim(),
      notes: notes,
    );
  }
}
