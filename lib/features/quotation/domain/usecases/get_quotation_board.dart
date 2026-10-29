import '../../../../core/base/paginated.dart';
import '../entities/quotation.dart';
import '../repositories/quotation_repository.dart';

/// One page of the Quotations board, read from `GET /quotations`.
class GetQuotationBoard {
  static const pageSize = 50;

  final QuotationRepository _quotations;

  const GetQuotationBoard(this._quotations);

  Future<Paginated<Quotation>> call({int page = 1}) async {
    final rows = await _quotations.list(page: page, pageSize: pageSize);
    return Paginated.page(items: rows, page: page, pageSize: pageSize);
  }
}
