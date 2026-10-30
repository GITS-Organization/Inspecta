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
import '../../domain/entities/quote_line_item.dart';
import '../../domain/entities/reply_type.dart';
import '../../domain/proposal_terms.dart';
import '../../domain/usecases/get_quote_pdf.dart';
import '../../domain/usecases/save_draft.dart';
import '../../domain/usecases/send_quotation.dart';
import '../../domain/usecases/update_offer.dart';

part 'quotation_form_state.dart';

/// The reply panel on a request's detail screen (Feature 03 §5): reply
/// type, price, validity, message, their validation, and actually
/// saving/sending them — including reject, which now goes through the
/// same [SendQuotation] path so the Quotations tracker has a full
/// history of every reply sent.
///
/// Known gaps: Accept has no real price (no price list backend, US-03's
/// open question #1), there's no VAT line (the proposal just states that
/// 14% VAT is added), and nothing here emails the client the proposal PDF
/// (BR-03.5) — that needs a backend job this client can't perform, so for
/// now it is only previewed.
class QuotationFormCubit extends Cubit<QuotationFormState> {
  final InspectionRequest request;

  /// The quote this reply is for, when the request already has one. An offer
  /// is then saved onto it through the API and its PDF comes from there.
  final String? quotationId;
  final SaveDraft _saveDraft;
  final SendQuotation _sendQuotation;
  final UpdateOffer _updateOffer;
  final GetQuotePdf _getQuotePdf;
  final GetRequestDetail _getRequestDetail;

  /// The request's real items, fetched the first time the offer is saved.
  List<RequestItem>? _realItems;

  QuotationFormCubit({
    required this.request,
    required this.quotationId,
    required SaveDraft saveDraft,
    required SendQuotation sendQuotation,
    required UpdateOffer updateOffer,
    required GetQuotePdf getQuotePdf,
    required GetRequestDetail getRequestDetail,
  }) : _saveDraft = saveDraft,
       _sendQuotation = sendQuotation,
       _updateOffer = updateOffer,
       _getQuotePdf = getQuotePdf,
       _getRequestDetail = getRequestDetail,
       super(QuotationFormState(itemIds: request.items.map((item) => item.id).toList()));

  /// Whether the offer goes through the API's quote (it has an id to save
  /// onto), rather than the older draft path.
  bool get hasQuote => quotationId != null;

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

  /// "Overhead crane × 2", or each item type joined when there's more
  /// than one — shown on the Quotations tracker's cards.
  String get _equipmentSummary {
    if (request.items.isEmpty) return request.equipmentTitle;
    return request.items.map((item) => '${item.type} × ${item.quantity}').join(', ');
  }

  void selectReplyType(ReplyType type) {
    emit(state.copyWith(replyType: type, submitted: false, clearFailure: true));
  }

  void changeLinePrice(String itemId, String value) =>
      emit(state.copyWith(linePrices: {...state.linePrices, itemId: value}));

  void changeScopeOfWork(List<String> value) => emit(state.copyWith(scopeOfWork: value));

  void changeNotes(List<String> value) => emit(state.copyWith(notes: value));

  void changeValidity(int days) => emit(state.copyWith(validityDays: days));

  void changeMessage(String value) => emit(state.copyWith(message: value));

  /// The offer's lines, each at its own entered price, per visit.
  List<QuoteLineItem> offerLines({bool priced = true}) {
    return request.items
        .map(
          (item) => QuoteLineItem(
            requestItemId: item.id,
            type: item.type,
            quantity: item.quantity,
            unitPricePiastres: priced ? (state.unitPriceOf(item.id) ?? 0) : 0,
            pricingUnit: PricingUnit.perVisit,
          ),
        )
        .toList();
  }

  /// US-03.4: not available once Reject is chosen — there's nothing
  /// meaningful to draft.
  Future<void> saveDraft() async {
    if (state.replyType == null || state.replyType == ReplyType.reject) return;
    if (state.isSubmitting) return;

    emit(state.copyWith(isSubmitting: true));
    try {
      if (state.isPriced && hasQuote) {
        await saveOffer();
      } else {
        await _saveDraft(
          requestId: request.id,
          requestNumber: request.id,
          clientName: request.clientName,
          equipmentSummary: _equipmentSummary,
          type: state.replyType!,
          items: state.isPriced
              ? offerLines()
              : offerLines(priced: false),
          validityDays: state.isPriced ? state.validityDays : null,
          message: state.message,
          notes: state.isPriced ? notes : null,
          scopeOfWork: state.isPriced ? scopeOfWork : null,
        );
      }
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
      if (state.isPriced && hasQuote) await saveOffer();
      final version = await _sendQuotation(
        requestId: request.id,
        requestNumber: request.id,
        clientName: request.clientName,
        equipmentSummary: _equipmentSummary,
        type: state.replyType!,
        items: state.isPriced
            ? offerLines()
            : offerLines(priced: false),
        quotationId: quotationId,
        validityDays: state.isPriced ? state.validityDays : null,
        message: state.message,
        notes: state.isPriced ? notes : null,
        scopeOfWork: state.isPriced ? scopeOfWork : null,
      );
      emit(state.copyWith(sentVersion: version));
      _completeAction(QuotationAction.send, success: true);
    } catch (e) {
      _completeAction(QuotationAction.send, success: false, failure: _mapFailure(e));
    }
  }

  /// [_saveDraft]/[_sendQuotation] throw [QuotationFailure]; a reject
  /// send also touches the requests collection and can throw
  /// [RequestsFailure]. Both map onto the same failure codes for display.
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
