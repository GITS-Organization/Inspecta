import '../entities/quotation.dart';
import '../repositories/quotation_repository.dart';

/// Sends a request's next quote version at a new total price, from the quote
/// detail screen's "Send new price".
class SendNewPrice {
  final QuotationRepository _repository;

  const SendNewPrice(this._repository);

  Future<Quotation> call({
    required String requestId,
    required int totalPiastres,
    int? validityDays,
  }) {
    return _repository.sendNewPrice(
      requestId: requestId,
      totalPiastres: totalPiastres,
      validityDays: validityDays,
    );
  }
}
