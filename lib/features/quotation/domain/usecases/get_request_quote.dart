import '../entities/quotation.dart';
import '../entities/quotation_status.dart';
import '../repositories/quotation_repository.dart';

/// The draft quote the API keeps for a request that hasn't been replied to
/// yet, read from `GET /quotations` — the reply form needs its id to save the
/// offer onto it, preview its email and send or accept it. Null when the
/// request has no draft (a countered request is answered with a new version
/// instead). Throws [QuotationFailure] when the list can't be read.
class GetRequestQuote {
  static const _pageSize = 50;

  /// The newest quotes come first and a draft is new, so it is found within
  /// the first pages; looking further than this isn't worth the requests.
  static const _maxPages = 10;

  final QuotationRepository _quotations;

  const GetRequestQuote(this._quotations);

  Future<Quotation?> call(String requestId) async {
    for (var page = 1; page <= _maxPages; page++) {
      final rows = await _quotations.list(page: page, pageSize: _pageSize);
      for (final row in rows) {
        if (row.requestId == requestId && row.status == QuotationStatus.draft) return row;
      }
      if (rows.length < _pageSize) return null;
    }
    return null;
  }
}
