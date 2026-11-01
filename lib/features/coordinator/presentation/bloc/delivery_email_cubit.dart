import 'package:equatable/equatable.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/utils/validators.dart';
import '../../../machines/domain/entities/delivery.dart';
import '../../../machines/domain/entities/delivery_failure.dart';
import '../../../machines/domain/usecases/deliver_to_client.dart';
import '../../../machines/domain/usecases/get_delivery_preview.dart';
import '../../../requests/domain/entities/requests_failure.dart';

part 'delivery_email_state.dart';

/// Backs the screen where the coordinator reads the email that goes to the
/// client, edits it (To, Cc, subject, text) and sends it with the job's
/// certificates attached.
///
/// Nothing is saved while editing: Send posts only the parts that differ from
/// the preview, and the job becomes sent to client.
class DeliveryEmailCubit extends Cubit<DeliveryEmailState> {
  /// The most addresses the API takes in Cc.
  static const maxCc = 10;

  final String requestId;
  final GetDeliveryPreview _getPreview;
  final DeliverToClient _deliver;

  DeliveryEmailCubit({
    required this.requestId,
    required GetDeliveryPreview getDeliveryPreview,
    required DeliverToClient deliverToClient,
  }) : _getPreview = getDeliveryPreview,
       _deliver = deliverToClient,
       super(const DeliveryEmailState());

  Future<void> start() async {
    try {
      final preview = await _getPreview(requestId);
      if (isClosed) return;
      emit(
        state.copyWith(
          status: DeliveryEmailStatus.ready,
          preview: preview,
          to: preview.to ?? '',
          cc: preview.cc,
          subject: preview.subject,
          body: preview.body,
        ),
      );
    } catch (error) {
      if (isClosed) return;
      emit(
        state.copyWith(
          status: DeliveryEmailStatus.error,
          loadFailure: error is RequestsFailure ? error.code : RequestsFailureCode.unknown,
        ),
      );
    }
  }

  Future<void> retry() {
    emit(state.copyWith(status: DeliveryEmailStatus.loading));
    return start();
  }

  void setTo(String value) => emit(state.copyWith(to: value));

  void setSubject(String value) => emit(state.copyWith(subject: value));

  void setBody(String value) => emit(state.copyWith(body: value));

  /// Adds the address(es) in [raw] to Cc — one, or several separated by
  /// commas, semicolons or spaces. Returns false, adding nothing, when one of
  /// them doesn't look like an address.
  bool addCc(String raw) {
    final entries = raw.split(RegExp(r'[,;\s]+')).where((e) => e.isNotEmpty).toList();
    if (entries.isEmpty) return true;
    if (!entries.every(Validators.isEmail)) return false;

    final cc = [...state.cc];
    for (final entry in entries) {
      final known = cc.any((a) => a.toLowerCase() == entry.toLowerCase());
      if (!known && cc.length < maxCc) cc.add(entry);
    }
    emit(state.copyWith(cc: cc));
    return true;
  }

  void removeCc(String address) => emit(state.copyWith(cc: [...state.cc]..remove(address)));

  Future<void> send() async {
    final preview = state.preview;
    if (preview == null || !state.canSend) return;
    emit(state.copyWith(isSending: true));

    final to = state.to.trim();
    final subject = state.subject.trim();
    final body = state.body.trim();
    try {
      // Only what was edited goes up; the API fills in the rest from the
      // preview.
      final result = await _deliver(
        requestId,
        sentTo: to == (preview.to ?? '') ? null : to,
        cc: listEquals(state.cc, preview.cc) ? null : state.cc,
        subject: subject == preview.subject.trim() ? null : subject,
        message: body == preview.body.trim() ? null : body,
      );
      if (isClosed) return;
      emit(
        state.copyWith(
          isSending: false,
          actionSeq: state.actionSeq + 1,
          lastActionSuccess: true,
          lastResult: result,
        ),
      );
    } catch (error) {
      if (isClosed) return;
      emit(
        state.copyWith(
          isSending: false,
          actionSeq: state.actionSeq + 1,
          lastActionSuccess: false,
          lastActionFailure: error is DeliveryFailure ? error.code : DeliveryFailureCode.unknown,
        ),
      );
    }
  }
}
