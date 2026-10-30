import 'dart:typed_data';

import '../repositories/quotation_repository.dart';

/// The proposal PDF the API generates for a quote.
class GetQuotePdf {
  final QuotationRepository _repository;

  const GetQuotePdf(this._repository);

  Future<Uint8List> call({required String requestId, required String quotationId}) {
    return _repository.getPdf(requestId: requestId, quotationId: quotationId);
  }
}
