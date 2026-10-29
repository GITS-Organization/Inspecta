import 'package:flutter/foundation.dart';

import '../../../../core/base/paginated.dart';
import '../../../requests/domain/entities/request_triage.dart';
import '../../../requests/domain/repositories/requests_repository.dart';
import '../entities/quotation.dart';
import '../entities/quotation_status.dart';
import '../repositories/quotation_repository.dart';

/// One page of the Quotations board, read from the API: a page of the
/// coordinator's requests, then `GET /requests/{id}/quotes` for each, keeping
/// every request's current quote. The API lists quotes per request only, so
/// the board is built request by request.
class GetQuotationBoard {
  static const pageSize = 20;

  final RequestsRepository _requests;
  final QuotationRepository _quotations;

  const GetQuotationBoard(this._requests, this._quotations);

  Future<Paginated<Quotation>> call({int page = 1}) async {
    final requests = await _requests.listRequests(
      triage: RequestTriage.request,
      page: page,
      pageSize: pageSize,
    );

    // Every request is asked: the API doesn't say which have quotes, and a
    // request's own status can't be relied on to show it.
    final results = await Future.wait([
      for (final request in requests.items) _currentQuote(request.id),
    ]);

    // One request's quotes failing is skipped; all of them failing is the
    // page failing (no connection, signed out).
    final failure = results.where((r) => r.failure != null).firstOrNull?.failure;
    if (failure != null && results.every((r) => r.failure != null)) throw failure;

    return Paginated(
      items: [
        for (final r in results)
          if (r.quote != null) r.quote!,
      ],
      page: page,
      hasMore: requests.hasMore,
    );
  }

  /// The newest version of [requestId]'s quotes that is neither a draft nor
  /// superseded, or a failure if they couldn't be loaded.
  Future<({Quotation? quote, Object? failure})> _currentQuote(String requestId) async {
    try {
      final versions = await _quotations.getForRequest(requestId);
      return (
        quote: versions
            .where(
              (q) =>
                  q.status != QuotationStatus.draft &&
                  q.status != QuotationStatus.superseded,
            )
            .firstOrNull,
        failure: null,
      );
    } catch (error) {
      debugPrint('Skipping quotes of request $requestId: $error');
      return (quote: null, failure: error);
    }
  }
}
