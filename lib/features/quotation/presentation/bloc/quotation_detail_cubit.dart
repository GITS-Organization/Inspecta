import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/utils/currency.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/usecases/get_request_detail.dart';
import '../../domain/entities/client_response_outcome.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_failure.dart';
import '../../domain/entities/quotation_status.dart';
import '../../domain/entities/quote_email.dart';
import '../../domain/entities/reply_type.dart';
import '../../domain/usecases/dismiss_client_reply.dart';
import '../../domain/usecases/get_request_quotes.dart';
import '../../domain/usecases/log_client_response.dart';
import '../../domain/usecases/reopen_quotation.dart';
import '../../domain/usecases/send_new_price.dart';
import '../../domain/usecases/send_quotation.dart';

part 'quotation_detail_state.dart';

/// The quote detail screen: the quote that was opened, the client's replies to
/// it, and what the coordinator can do next — send a new price, reopen it to
/// edit, accept, reject, or ignore a reply that needs no action.
///
/// The quote comes from the Quotations list row that was tapped, so the
/// screen opens at once; the request's quotes (with the client's replies) are
/// read in the background and replace the row when they arrive. "Send
/// reminder" lives on the tracker's cards instead — it's a one-tap action,
/// not something that needs this screen open.
class QuotationDetailCubit extends Cubit<QuotationDetailState> {
  final String requestId;
  final LogClientResponse _logClientResponse;
  final SendNewPrice _sendNewPrice;
  final SendQuotation _sendQuotation;
  final GetRequestQuotes _getRequestQuotes;
  final GetRequestDetail _getRequestDetail;
  final ReopenQuotation _reopenQuotation;
  final DismissClientReply _dismissClientReply;

  /// [quotation] is the list row that was opened; [request] is the request
  /// when the caller already has it (opened from the request screen).
  QuotationDetailCubit({
    required this.requestId,
    required Quotation? quotation,
    required InspectionRequest? request,
    required LogClientResponse logClientResponse,
    required SendNewPrice sendNewPrice,
    required SendQuotation sendQuotation,
    required GetRequestQuotes getRequestQuotes,
    required GetRequestDetail getRequestDetail,
    required ReopenQuotation reopenQuotation,
    required DismissClientReply dismissClientReply,
  }) : _logClientResponse = logClientResponse,
       _sendNewPrice = sendNewPrice,
       _sendQuotation = sendQuotation,
       _getRequestQuotes = getRequestQuotes,
       _getRequestDetail = getRequestDetail,
       _reopenQuotation = reopenQuotation,
       _dismissClientReply = dismissClientReply,
       super(
         QuotationDetailState(
           status: QuotationDetailStatus.ready,
           versions: quotation == null ? const [] : [quotation],
           request: request,
         ),
       ) {
    // A new request has no quote to read yet: its reply form is the screen.
    if (quotation != null || !(request?.isNew ?? false)) unawaited(refresh());
  }

  /// Reads the request's quotes again — with the client's replies — and keeps
  /// what is on screen when that fails.
  Future<void> refresh() async {
    try {
      final quotes = await _getRequestQuotes(requestId);
      if (!isClosed && quotes.isNotEmpty) emit(state.copyWith(versions: quotes));
    } catch (_) {
      // The row that was opened stays.
    }
  }

  /// The request as it is now (its status changes with every action here).
  Future<void> _refreshRequest() async {
    try {
      final request = await _getRequestDetail(requestId);
      if (!isClosed && request != null) emit(state.copyWith(request: request));
    } catch (_) {
      // The earlier copy stays.
    }
  }

  /// Runs [action] with the screen locked, then reads the quotes and the
  /// request again. Returns why it failed, or `null` when it went through.
  Future<QuotationFailureCode?> _act(Future<void> Function() action) async {
    if (state.isSubmitting) return null;
    emit(state.copyWith(isSubmitting: true));
    try {
      await action();
    } catch (e) {
      if (!isClosed) emit(state.copyWith(isSubmitting: false));
      return e is QuotationFailure ? e.code : QuotationFailureCode.unknown;
    }
    await Future.wait([refresh(), _refreshRequest()]);
    if (!isClosed) emit(state.copyWith(isSubmitting: false));
    return null;
  }

  /// Sends the next quote version at [totalPiastres] (a counter offer) and puts
  /// it on top of the screen. Returns why it failed, or `null` when it went
  /// through.
  Future<QuotationFailureCode?> sendNewPrice({
    required int totalPiastres,
    int? validityDays,
  }) async {
    try {
      final sent = await _sendNewPrice(
        requestId: requestId,
        totalPiastres: totalPiastres,
        // The new version is priced in the quote's own currency.
        currency: state.current?.currency ?? Currency.egp,
        validityDays: validityDays,
      );
      if (!isClosed) emit(state.copyWith(versions: [sent, ...state.versions]));
      unawaited(_refreshRequest());
      return null;
    } catch (e) {
      return e is QuotationFailure ? e.code : QuotationFailureCode.unknown;
    }
  }

  /// Opens a new revision of the sent quote, with the same prices, to edit and
  /// send again. The screen then shows the revision as a draft.
  Future<QuotationFailureCode?> reopen() {
    final quote = state.current;
    if (quote == null) return Future.value(QuotationFailureCode.unknown);
    return _act(() async {
      final reopened = await _reopenQuotation(requestId: requestId, quotationId: quote.id);
      // Shown straight away; the refresh then brings the whole list.
      if (!isClosed) emit(state.copyWith(versions: [reopened, ...state.versions]));
    });
  }

  /// Ignores the client's reply: it is cleared and the request waits for the
  /// client again.
  Future<QuotationFailureCode?> dismissReply() {
    final quote = state.current;
    if (quote == null) return Future.value(QuotationFailureCode.unknown);
    return _act(() async {
      final updated = await _dismissClientReply(requestId: requestId, quotationId: quote.id);
      if (!isClosed) {
        emit(
          state.copyWith(
            versions: [for (final v in state.versions) v.id == updated.id ? updated : v],
          ),
        );
      }
    });
  }

  /// Accepts the quote; the API emails the client the PDF, as suggested or as
  /// edited in [email].
  Future<QuotationFailureCode?> accept({QuoteEmailEdits? email}) {
    final quote = state.current;
    if (quote == null) return Future.value(QuotationFailureCode.unknown);
    return _act(
      () => _sendQuotation(
        requestId: requestId,
        type: ReplyType.accept,
        quotationId: quote.id,
        email: email,
      ).then((_) {}),
    );
  }

  /// Rejects the request, telling the client [reason].
  Future<QuotationFailureCode?> reject(String reason) => _act(
    () => _sendQuotation(
      requestId: requestId,
      type: ReplyType.reject,
      message: reason,
    ).then((_) {}),
  );

  Future<void> logResponse({
    required ClientResponseOutcome outcome,
    int? clientPricePiastres,
    String? note,
  }) async {
    final quotation = state.current;
    if (quotation == null || state.isSubmitting) return;

    emit(state.copyWith(isSubmitting: true));
    try {
      await _logClientResponse(
        quotationId: quotation.id,
        requestId: requestId,
        outcome: outcome,
        clientPricePiastres: clientPricePiastres,
        note: note,
      );
      _complete(success: true);
    } catch (e) {
      _complete(
        success: false,
        failure: e is QuotationFailure ? e.code : QuotationFailureCode.unknown,
      );
    }
  }

  void _complete({required bool success, QuotationFailureCode? failure}) {
    if (isClosed) return;
    emit(
      state.copyWith(
        isSubmitting: false,
        actionSeq: state.actionSeq + 1,
        lastActionSuccess: success,
        lastActionFailure: failure,
        clearFailure: failure == null,
      ),
    );
  }
}
