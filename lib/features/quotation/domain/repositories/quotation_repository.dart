import 'dart:typed_data';

import '../../../../core/utils/currency.dart';
import '../entities/client_response_outcome.dart';
import '../entities/quotation.dart';
import '../entities/quote_email.dart';
import '../entities/quote_line_item.dart';
import '../entities/reply_type.dart';

/// Methods emit / throw [QuotationFailure] on expected errors.
abstract interface class QuotationRepository {
  /// One page of the user's quotations from the API across all requests —
  /// backs the Quotations tab. Superseded versions are left out unless
  /// [includeSuperseded]. Rows carry no line items. Throws [QuotationFailure].
  Future<List<Quotation>> list({
    int page = 1,
    int pageSize = 50,
    bool includeSuperseded = false,
  });

  /// BR-03.4/03.5: turns the request's quote into its next immutable,
  /// numbered version and marks it sent. Returns the assigned version. A
  /// [ReplyType.reject] is not a quote: it goes through
  /// `RequestsRepository.rejectRequest` instead, and passing it here throws
  /// an [ArgumentError].
  ///
  /// With a [quotationId], an offer sends that existing quote and an accept
  /// accepts it: either emails the PDF to the client — as suggested, or as
  /// edited in [email]. Throws a [QuotationFailure] (`invalidAddress`,
  /// `mailRefused`…) when the email can't go out.
  Future<int> send({
    required String requestId,
    required ReplyType type,
    String? quotationId,
    String currency = Currency.egp,
    int? validityDays,
    QuoteEmailEdits? email,
  });

  /// The email that would carry quote [quotationId]'s PDF — for accepting it
  /// when [accept], otherwise for sending the offer. Sends nothing. Throws
  /// [QuotationFailure].
  Future<QuoteEmailPreview> getEmailPreview({
    required String requestId,
    required String quotationId,
    required bool accept,
  });

  /// Saves the priced offer onto quote [quotationId] (its unit prices,
  /// validity, scope, notes and message) through the API. Throws
  /// [QuotationFailure].
  Future<void> updateOffer({
    required String requestId,
    required String quotationId,
    required List<QuoteLineItem> items,
    String currency = Currency.egp,
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
    String currency = Currency.egp,
    int? validityDays,
  });

  /// BR-03.6: records the client's answer on [quotationId]. When
  /// [outcome] is accepted, every other non-superseded quotation for
  /// [requestId] is marked superseded (BR-03.7).
  ///
  /// Not available yet: the API has no endpoint for it, so this always throws
  /// a [QuotationFailure].
  Future<void> recordClientResponse({
    required String quotationId,
    required String requestId,
    required ClientResponseOutcome outcome,
    int? clientPricePiastres,
    String? note,
  });
}
