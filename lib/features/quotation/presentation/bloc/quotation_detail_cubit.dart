import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../requests/domain/entities/inspection_request.dart';
import '../../domain/entities/client_response_outcome.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_failure.dart';
import '../../domain/entities/quotation_status.dart';
import '../../domain/usecases/log_client_response.dart';
import '../../domain/usecases/send_new_price.dart';

part 'quotation_detail_state.dart';

/// The quote detail screen: the quote that was opened, plus logging the
/// client's reply (BR-03.6/03.7). The quote comes from the Quotations list
/// row that was tapped, so opening the screen makes no request. "Send
/// reminder" lives on the tracker's cards instead — it's a one-tap action,
/// not something that needs this screen open.
class QuotationDetailCubit extends Cubit<QuotationDetailState> {
  final String requestId;
  final LogClientResponse _logClientResponse;
  final SendNewPrice _sendNewPrice;

  /// [quotation] is the list row that was opened; [request] is the request
  /// when the caller already has it (opened from the request screen).
  QuotationDetailCubit({
    required this.requestId,
    required Quotation? quotation,
    required InspectionRequest? request,
    required LogClientResponse logClientResponse,
    required SendNewPrice sendNewPrice,
  }) : _logClientResponse = logClientResponse,
       _sendNewPrice = sendNewPrice,
       super(
         QuotationDetailState(
           status: QuotationDetailStatus.ready,
           versions: quotation == null ? const [] : [quotation],
           request: request,
         ),
       );

  /// Sends the next version at [totalPiastres] and puts it on top of the
  /// screen. Returns why it failed, or `null` when it went through.
  Future<QuotationFailureCode?> sendNewPrice({
    required int totalPiastres,
    int? validityDays,
  }) async {
    try {
      final sent = await _sendNewPrice(
        requestId: requestId,
        totalPiastres: totalPiastres,
        validityDays: validityDays,
      );
      if (!isClosed) emit(state.copyWith(versions: [sent, ...state.versions]));
      return null;
    } catch (e) {
      return e is QuotationFailure ? e.code : QuotationFailureCode.unknown;
    }
  }

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
