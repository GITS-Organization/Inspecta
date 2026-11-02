import 'dart:convert';
import 'dart:typed_data';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/utils/currency.dart';
import '../../../requests/domain/entities/inspection_request.dart';
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
/// The offer is saved onto the request's quote, which is then sent or
/// accepted through its own endpoints. The quote is the one the form was
/// opened with or the one the request itself reports
/// ([InspectionRequest.draftQuoteId]); the form never opens one (that happens
/// when the request's details are saved). When this copy of the request has
/// none, the request is read again the first time a reply needs it. A form
/// built from a Quotations list row reads the request as it opens, for its
/// real items. Saving an offer that is already what the quote holds (saved
/// from this form and not changed since) costs no request.
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
  final GetRequestDetail _getRequestDetail;

  String? _quotationId;

  /// The look-up of the request's quote, while it runs.
  Future<String>? _finding;

  /// The request as the API has it, read when the form opens for a request
  /// built from a Quotations list row (see [_provisional]), whose real items
  /// the save needs.
  Future<InspectionRequest?>? _detail;

  /// What the last successful save put on the quote (see [_offerKey]); a save
  /// of the same offer is skipped.
  String? _savedOffer;

  QuotationFormCubit({
    required this.request,
    required String? quotationId,
    required SendQuotation sendQuotation,
    required UpdateOffer updateOffer,
    required GetQuotePdf getQuotePdf,
    required GetQuoteEmail getQuoteEmail,
    required GetRequestDetail getRequestDetail,
  }) : _quotationId = quotationId ?? request.draftQuoteId,
       _sendQuotation = sendQuotation,
       _updateOffer = updateOffer,
       _getQuotePdf = getQuotePdf,
       _getQuoteEmail = getQuoteEmail,
       _getRequestDetail = getRequestDetail,
       super(QuotationFormState(itemIds: request.items.map((item) => item.id).toList())) {
    // Read now, so that saving the offer (and so Accept or Send) waits for
    // nothing.
    if (_provisional) _readDetail().ignore();
  }

  /// The quote this reply is for, when the request already has one. An offer
  /// is then saved onto it through the API and its PDF comes from there.
  String? get quotationId => _quotationId;

  /// Whether the request already has a quote the offer can be saved onto.
  bool get hasQuote => _quotationId != null;

  /// The id of the quote the reply is priced on. A request screen left open
  /// since before its quote was opened doesn't know it yet, so the request is
  /// read again for it; no quote is created here. Concurrent callers share the
  /// one look-up. Throws [QuotationFailure].
  Future<String> _ensureQuote() async {
    final known = _quotationId;
    if (known != null) return known;
    final finding = _finding ??= _findQuote();
    try {
      return _quotationId = await finding;
    } finally {
      _finding = null;
    }
  }

  Future<String> _findQuote() async {
    final String? found;
    try {
      found = (await _getRequestDetail(request.id))?.draftQuoteId;
    } catch (e) {
      throw QuotationFailure(_mapFailure(e));
    }
    if (found == null) throw const QuotationFailure(QuotationFailureCode.unknown);
    return found;
  }

  /// The request as the API has it. One read is shared; a failed one is
  /// forgotten, so the next caller tries again.
  Future<InspectionRequest?> _readDetail() {
    final read = _detail ??= _getRequestDetail(request.id);
    return read.catchError((Object error) {
      if (identical(_detail, read)) _detail = null;
      throw error;
    });
  }

  /// Whether [request] was built from a Quotations list row, which has no
  /// equipment lines: it holds a single stand-in line, so the form asks for
  /// one price per unit. The real items are read when the form opens.
  bool get _provisional => request.items.length == 1 && request.items.first.id.isEmpty;

  /// The offer's lines as the API takes them: for a provisional request, the
  /// one price per unit applied to each of the request's real items.
  Future<List<QuoteLineItem>> _apiLines() async {
    final lines = offerLines();
    if (!_provisional) return lines;

    final items = (await _readDetail())?.items;
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

  /// Everything [saveOffer] puts on the quote, as one comparable value.
  String _offerKey() => jsonEncode({
    'prices': [for (final id in state.itemIds) state.linePrices[id]],
    'currency': state.currency,
    'validity': state.validityDays,
    'scope': scopeOfWork,
    'notes': notes,
    'message': state.message,
  });

  /// Saves the offer as it stands onto the request's quote through the API;
  /// an offer that is already what the quote holds (saved earlier from this
  /// form and not changed since) costs no request. Throws [QuotationFailure].
  Future<void> saveOffer() async {
    final key = _offerKey();
    if (key == _savedOffer) return;
    final quotationId = await _ensureQuote();
    await _updateOffer(
      requestId: request.id,
      quotationId: quotationId,
      items: await _apiLines(),
      currency: state.currency,
      validityDays: state.validityDays,
      scopeOfWork: scopeOfWork,
      notes: notes,
      message: state.message,
    );
    _savedOffer = key;
  }

  /// The proposal PDF the API generates for the quote. Save the offer first,
  /// so it reflects what is on screen.
  Future<Uint8List> fetchPdf() async {
    return _getQuotePdf(requestId: request.id, quotationId: await _ensureQuote());
  }

  /// The email that will carry the PDF to the client. The offer is saved
  /// first, so the email (its machines, total and validity) reflects what is
  /// on screen. Throws [QuotationFailure].
  Future<QuoteEmailPreview> loadEmailPreview() async {
    await saveOffer();
    return _getQuoteEmail(
      requestId: request.id,
      quotationId: await _ensureQuote(),
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
  /// Reject is chosen — there's nothing meaningful to draft.
  Future<void> saveDraft() async {
    if (state.replyType == null || state.replyType == ReplyType.reject) return;
    if (state.isSubmitting) return;

    emit(state.copyWith(isSubmitting: true));
    try {
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
      // The prices go onto the quote first; sending (or accepting) then
      // releases it.
      if (state.isPriced) await saveOffer();
      // The email goes out as the coordinator reviewed it; when they never
      // opened it the request carries no body and the API sends its own
      // suggestion, so nothing is read here.
      final email = state.isPriced ? state.emailEdits : null;
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
