import '../entities/client_response_outcome.dart';
import '../entities/quotation.dart';
import '../entities/quote_line_item.dart';
import '../entities/reply_type.dart';

/// Methods emit / throw [QuotationFailure] on expected errors.
abstract interface class QuotationRepository {
  /// Every quotation (drafts and sent), newest first — backs the
  /// Quotations tab.
  Stream<List<Quotation>> watchAll();

  /// A request's own quotations from the API, newest version first — its
  /// version history, for the quote detail timeline. Throws
  /// [QuotationFailure].
  Future<List<Quotation>> getForRequest(String requestId);

  /// Creates the request's draft if none exists yet, otherwise updates
  /// the existing one (US-03.4). Not used for [ReplyType.reject] — see
  /// [send].
  Future<Quotation> saveDraft({
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
  });

  /// BR-03.4/03.5/03.8: turns the request's current draft (or creates one
  /// directly) into the next immutable, numbered version, and marks it
  /// sent — for all three [ReplyType]s, including reject, so the tracker
  /// has a full history. Returns the assigned version.
  Future<int> send({
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
  });

  /// BR-03.6: records the client's answer on [quotationId]. When
  /// [outcome] is accepted, every other non-superseded quotation for
  /// [requestId] is marked superseded (BR-03.7).
  Future<void> recordClientResponse({
    required String quotationId,
    required String requestId,
    required ClientResponseOutcome outcome,
    int? clientPricePiastres,
    String? note,
  });
}
