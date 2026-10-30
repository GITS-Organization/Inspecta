import 'dart:typed_data';

import '../entities/client_response_outcome.dart';
import '../entities/quotation.dart';
import '../entities/quote_line_item.dart';
import '../entities/reply_type.dart';

/// Methods emit / throw [QuotationFailure] on expected errors.
abstract interface class QuotationRepository {
  /// Every quotation (drafts and sent), newest first — backs the
  /// Quotations tab.
  Stream<List<Quotation>> watchAll();

  /// One page of the user's quotations from the API across all requests,
  /// without superseded versions — backs the Quotations tab. Rows carry no
  /// line items. Throws [QuotationFailure].
  Future<List<Quotation>> list({int page = 1, int pageSize = 50});

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
  ///
  /// With a [quotationId], an offer sends that existing quote through the
  /// API instead of creating one.
  Future<int> send({
    required String requestId,
    required String requestNumber,
    required String clientName,
    required String equipmentSummary,
    required ReplyType type,
    required List<QuoteLineItem> items,
    String? quotationId,
    int? validityDays,
    String? message,
    List<String>? notes,
    List<String>? scopeOfWork,
  });

  /// Saves the priced offer onto quote [quotationId] (its unit prices,
  /// validity, scope, notes and message) through the API. Throws
  /// [QuotationFailure].
  Future<void> updateOffer({
    required String requestId,
    required String quotationId,
    required List<QuoteLineItem> items,
    int? validityDays,
    List<String>? scopeOfWork,
    List<String>? notes,
    String? message,
  });

  /// The proposal PDF the API generates for quote [quotationId]. Throws
  /// [QuotationFailure].
  Future<Uint8List> getPdf({required String requestId, required String quotationId});

  /// Sends the request's next quote version at a new [totalPiastres] and
  /// returns it as the API recorded it. Throws [QuotationFailure].
  Future<Quotation> sendNewPrice({
    required String requestId,
    required int totalPiastres,
    int? validityDays,
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
