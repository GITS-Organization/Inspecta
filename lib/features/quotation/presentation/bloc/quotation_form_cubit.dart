import 'dart:typed_data';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/utils/currency.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/request_item.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/domain/usecases/get_request_detail.dart';
import '../../domain/entities/pricing_unit.dart';
import '../../domain/entities/quotation_failure.dart';
import '../../domain/entities/quote_email.dart';
import '../../domain/entities/quote_line_item.dart';
import '../../domain/entities/reply_type.dart';
import '../../domain/proposal_terms.dart';
import '../../domain/usecases/get_quote_email.dart';
import '../../domain/usecases/get_quote_pdf.dart';
import '../../domain/usecases/get_request_quote.dart';
import '../../domain/usecases/send_quotation.dart';
import '../../domain/usecases/update_offer.dart';

part 'quotation_form_state.dart';

/// The reply panel on a request's detail screen (Feature 03 §5): reply
/// type, price, the email to the client (or, for a reject, the
/// reason), their validation, and actually
/// saving/sending them — including reject, which now goes through the
/// same [SendQuotation] path so the Quotations tracker has a full
/// history of every reply sent.
///
/// Sending an offer or accepting a quote emails the client the PDF through
/// the API; [loadEmailPreview] shows that email first and [setEmailEdits]
/// keeps the coordinator's changes to it, which go out with [send].
///
/// Opened without a quote (from the request screen), the form looks up the
/// request's draft quote itself, so the offer is saved onto it and sent or
/// accepted through its own endpoints rather than creating a new quote.
///
/// Known gaps: Accept has no real price (no price list backend, US-03's
/// open question #1), and there's no VAT line (the proposal just states that
/// 14% VAT is added).
class QuotationFormCubit extends Cubit<QuotationFormState> {
  final InspectionRequest request;

  final SendQuotation _sendQuotation;
  final UpdateOffer _updateOffer;
  final GetQuotePdf _getQuotePdf;
  final GetQuoteEmail _getQuoteEmail;
  final GetRequestQuote _getRequestQuote;
  final GetRequestDetail _getRequestDetail;

  String? _quotationId;

  /// The look-up of the request's draft quote, while it runs.
  Future<void>? _resolving;

  /// Why the look-up failed, when it did: sending without the quote would
  /// create a new one, so a send waits for a look-up that worked.
  QuotationFailure? _lookupFailure;

  /// The request's real items, fetched the first time the offer is saved.
  List<RequestItem>? _realItems;

  QuotationFormCubit({
    required this.request,
    required String? quotationId,
    required SendQuotation sendQuotation,
    required UpdateOffer updateOffer,
    required GetQuotePdf getQuotePdf,
    required GetQuoteEmail getQuoteEmail,
    required GetRequestQuote getRequestQuote,
    required GetRequestDetail getRequestDetail,
  }) : _quotationId = quotationId,
       _sendQuotation = sendQuotation,
       _updateOffer = updateOffer,
       _getQuotePdf = getQuotePdf,
       _getQuoteEmail = getQuoteEmail,
       _getRequestQuote = getRequestQuote,
       _getRequestDetail = getRequestDetail,
       super(
         QuotationFormState(
           itemIds: request.items.map((item) => item.id).toList(),
           isResolvingQuote: quotationId == null,
         ),
       ) {
    if (quotationId == null) _resolving = _resolveQuote();
  }

  /// The quote this reply is for, when the request already has one. An offer
  /// is then saved onto it through the API and its PDF comes from there.
  String? get quotationId => _quotationId;

  /// Whether the request has a quote the offer can be saved onto.
  bool get hasQuote => _quotationId != null;

  Future<void> _resolveQuote() async {
    try {
      _quotationId = (await _getRequestQuote(request.id))?.id;
      _lookupFailure = null;
    } catch (e) {
      _lookupFailure = e is QuotationFailure ? e : const QuotationFailure(QuotationFailureCode.unknown);
    }
    if (!isClosed) emit(state.copyWith(isResolvingQuote: false));
  }

  /// Waits for the quote look-up, so a priced reply never goes out before
  /// the form knows whether the request has a quote. A look-up that failed is
  /// tried once more; if it fails again, so does the reply — it can't tell a
  /// missing quote from an unreachable one. Throws [QuotationFailure].
  Future<void> _ensureQuote() async {
    await _resolving;
    if (_quotationId != null || _lookupFailure == null) return;
    await (_resolving = _resolveQuote());
    if (_quotationId == null && _lookupFailure != null) throw _lookupFailure!;
  }

  /// Whether [request] was built from a Quotations list row, which has no
  /// equipment lines: it holds a single stand-in line, so the form asks for
  /// one price per unit. The real items are fetched when the offer is saved.
  bool get _provisional => request.items.length == 1 && request.items.first.id.isEmpty;

  /// The offer's lines as the API takes them: for a provisional request, the
  /// one price per unit applied to each of the request's real items.
  Future<List<QuoteLineItem>> _apiLines() async {
    final lines = offerLines();
    if (!_provisional) return lines;

    final items = _realItems ??= (await _getRequestDetail(request.id))?.items;
    if (items == null || items.isEmpty) {
      throw const QuotationFailure(QuotationFailureCode.unknown);
    }
    final unitPrice = lines.first.unitPricePiastres;
    return [
      for (final item in items)
        QuoteLineItem(
          requestItemId: item.id,
          type: item.type,
          quantity: item.quantity,
          unitPricePiastres: unitPrice,
          pricingUnit: PricingUnit.perVisit,
        ),
    ];
  }

  /// Saves the offer as it stands onto the quote through the API. Throws
  /// [QuotationFailure].
  Future<void> saveOffer() async {
    return _updateOffer(
      requestId: request.id,
      quotationId: quotationId!,
      items: await _apiLines(),
      currency: state.currency,
      validityDays: state.validityDays,
      scopeOfWork: scopeOfWork,
      notes: notes,
      message: state.message,
    );
  }

  /// The proposal PDF the API generates for the quote. Save the offer first,
  /// so it reflects what is on screen.
  Future<Uint8List> fetchPdf() {
    return _getQuotePdf(requestId: request.id, quotationId: quotationId!);
  }

  /// The email that will carry the PDF to the client. The offer is saved
  /// first, so the email (its machines, total and validity) reflects what is
  /// on screen. Throws [QuotationFailure].
  Future<QuoteEmailPreview> loadEmailPreview() async {
    await saveOffer();
    return _getQuoteEmail(
      requestId: request.id,
      quotationId: quotationId!,
      accept: state.replyType == ReplyType.accept,
    );
  }

  /// Keeps the email the coordinator reviewed, to go out with Send.
  void setEmailEdits(QuoteEmailEdits? edits) {
    if (edits == null || edits.isEmpty) {
      emit(state.copyWith(clearEmailEdits: true));
    } else {
      emit(state.copyWith(emailEdits: edits));
    }
  }

  /// The API's suggested email as a full set of values, for a send where the
  /// coordinator didn't open the email. Null when it can't be read, in which
  /// case the send carries no body and the API uses its suggestion itself.
  Future<QuoteEmailEdits?> _suggestedEmail() async {
    try {
      final preview = await _getQuoteEmail(
        requestId: request.id,
        quotationId: quotationId!,
        accept: state.replyType == ReplyType.accept,
      );
      return QuoteEmailEdits(
        sentTo: preview.to,
        cc: preview.cc,
        subject: preview.subject,
        message: preview.body,
      );
    } catch (_) {
      return null;
    }
  }

  /// Sum of every line's price × quantity, or `null` until each line has a
  /// price.
  int? get totalPiastres {
    var total = 0;
    for (final item in request.items) {
      final unitPrice = state.unitPriceOf(item.id);
      if (unitPrice == null) return null;
      total += unitPrice * item.quantity;
    }
    return total;
  }

  /// The scope of work as it'll print: the points as edited, without blank ones.
  List<String> get scopeOfWork =>
      state.scopeOfWork.map((point) => point.trim()).where((point) => point.isNotEmpty).toList();

  /// The notes as they'll print: the points as edited, without blank ones.
  List<String> get notes =>
      state.notes.map((note) => note.trim()).where((note) => note.isNotEmpty).toList();

  // The email's text states the machines, the total and the validity (and
  // differs between an offer and an accept), so changing any of them drops
  // the coordinator's edits: sending a stale total would be worse than
  // editing the email again.
  void selectReplyType(ReplyType type) {
    emit(state.copyWith(replyType: type, submitted: false, clearFailure: true, clearEmailEdits: true));
  }

  void changeLinePrice(String itemId, String value) =>
      emit(state.copyWith(linePrices: {...state.linePrices, itemId: value}, clearEmailEdits: true));

  void changeScopeOfWork(List<String> value) => emit(state.copyWith(scopeOfWork: value));

  void changeNotes(List<String> value) => emit(state.copyWith(notes: value));


  /// Every price on the quote is in one currency: the amounts typed stay as
  /// they are and are now read in [code].
  void changeCurrency(String code) =>
      emit(state.copyWith(currency: Currency.normalize(code), clearEmailEdits: true));

  void changeMessage(String value) => emit(state.copyWith(message: value));

  /// The offer's lines, each at its own entered price, per visit.
  List<QuoteLineItem> offerLines() {
    return request.items
        .map(
          (item) => QuoteLineItem(
            requestItemId: item.id,
            type: item.type,
            quantity: item.quantity,
            unitPricePiastres: state.unitPriceOf(item.id) ?? 0,
            pricingUnit: PricingUnit.perVisit,
          ),
        )
        .toList();
  }

  /// US-03.4: saves the offer onto the request's quote. Not available once
  /// Reject is chosen — there's nothing meaningful to draft — nor while the
  /// request has no quote: the API creates one only by sending it.
  Future<void> saveDraft() async {
    if (state.replyType == null || state.replyType == ReplyType.reject) return;
    if (state.isSubmitting) return;

    emit(state.copyWith(isSubmitting: true));
    try {
      await _ensureQuote();
      if (!hasQuote) throw const QuotationFailure(QuotationFailureCode.unknown);
      await saveOffer();
      _completeAction(QuotationAction.saveDraft, success: true);
    } catch (e) {
      _completeAction(QuotationAction.saveDraft, success: false, failure: _mapFailure(e));
    }
  }

  /// BR-03.5/BR-03.8: validates first (acceptance criterion 2), then
  /// sends — accept/offer/reject alike.
  Future<void> send() async {
    if (state.isSubmitting) return;
    if (!validate()) return;

    emit(state.copyWith(isSubmitting: true));
    try {
      if (state.isPriced) await _ensureQuote();
      // The prices go onto the quote first; sending (or accepting) then
      // releases it.
      if (state.isPriced && hasQuote) await saveOffer();
      // The email goes out in full: as the coordinator reviewed it, or — when
      // they never opened it — as the API suggests it.
      final email = state.isPriced && hasQuote ? (state.emailEdits ?? await _suggestedEmail()) : null;
      final version = await _sendQuotation(
        requestId: request.id,
        type: state.replyType!,
        quotationId: quotationId,
        currency: state.currency,
        validityDays: state.isPriced ? state.validityDays : null,
        message: state.message,
        email: email,
      );
      emit(state.copyWith(sentVersion: version));
      _completeAction(QuotationAction.send, success: true);
    } catch (e) {
      _completeAction(QuotationAction.send, success: false, failure: _mapFailure(e));
    }
  }

  /// [saveOffer]/[_sendQuotation] throw [QuotationFailure]; a reject send
  /// goes through the requests API and can throw [RequestsFailure]. Both map
  /// onto the same failure codes for display.
  QuotationFailureCode _mapFailure(Object error) {
    if (error is QuotationFailure) return error.code;
    if (error is RequestsFailure) {
      return switch (error.code) {
        RequestsFailureCode.permissionDenied => QuotationFailureCode.permissionDenied,
        RequestsFailureCode.network => QuotationFailureCode.network,
        RequestsFailureCode.conflict || RequestsFailureCode.unknown => QuotationFailureCode.unknown,
      };
    }
    return QuotationFailureCode.unknown;
  }

  void _completeAction(
    QuotationAction action, {
    required bool success,
    QuotationFailureCode? failure,
  }) {
    if (isClosed) return;
    emit(
      state.copyWith(
        isSubmitting: false,
        actionSeq: state.actionSeq + 1,
        lastAction: action,
        lastActionSuccess: success,
        lastActionFailure: failure,
        clearFailure: failure == null,
      ),
    );
  }

  /// Flips on field errors (acceptance criterion 2) and reports whether
  /// the form may be sent.
  bool validate() {
    if (!state.submitted) emit(state.copyWith(submitted: true));
    return state.isValid;
  }
}
