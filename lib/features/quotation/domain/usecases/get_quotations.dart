import '../entities/quotation.dart';
import '../repositories/quotation_repository.dart';

/// Every quotation of the user, superseded versions included, read page by
/// page from `GET /quotations` — what the Profile's "This month" tiles count.
class GetQuotations {
  static const pageSize = 50;

  /// A stop in case the API keeps answering with full pages.
  static const _maxPages = 20;

  final QuotationRepository _repository;

  const GetQuotations(this._repository);

  Future<List<Quotation>> call() async {
    final quotations = <Quotation>[];
    for (var page = 1; page <= _maxPages; page++) {
      final rows = await _repository.list(
        page: page,
        pageSize: pageSize,
        includeSuperseded: true,
      );
      quotations.addAll(rows);
      if (rows.length < pageSize) break;
    }
    return quotations;
  }
}
