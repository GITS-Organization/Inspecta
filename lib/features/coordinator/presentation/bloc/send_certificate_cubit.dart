import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/utils/validators.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/domain/entities/certificate_failure.dart';
import '../../../certificate/domain/usecases/send_certificate_to_client.dart';
import '../../../certificate/domain/usecases/watch_certificate.dart';
import '../../../clients/domain/usecases/get_client_detail.dart';
import '../../../requests/domain/entities/inspection_request.dart';

part 'send_certificate_state.dart';

/// Backs the coordinator's "Send certificate" screen: an approved
/// certificate, the client address to send it to (the client's certificates
/// inbox to start with) and the one action that marks it as sent.
class SendCertificateCubit extends Cubit<SendCertificateState> {
  final InspectionRequest request;
  final WatchCertificate _watchCertificate;
  final GetClientDetail _getClientDetail;
  final SendCertificateToClient _send;

  StreamSubscription<Certificate?>? _subscription;

  SendCertificateCubit({
    required this.request,
    required WatchCertificate watchCertificate,
    required GetClientDetail getClientDetail,
    required SendCertificateToClient sendCertificateToClient,
  }) : _watchCertificate = watchCertificate,
       _getClientDetail = getClientDetail,
       _send = sendCertificateToClient,
       super(const SendCertificateState());

  void start() {
    if (_subscription != null) return;
    _subscription = _watchCertificate(request.id).listen(
      (certificate) => emit(
        state.copyWith(
          status: certificate == null ? SendCertificateStatus.notFound : SendCertificateStatus.ready,
          certificate: certificate,
        ),
      ),
      onError: (Object error) => emit(state.copyWith(status: SendCertificateStatus.error)),
    );
    _loadClientEmail();
  }

  /// Pre-fills the address with the client's certificates inbox — the
  /// coordinator can still change it.
  Future<void> _loadClientEmail() async {
    final clientId = request.clientId;
    if (clientId == null) return;
    try {
      final client = await _getClientDetail(clientId);
      if (isClosed) return;
      final email = client?.certificatesEmail ?? client?.mainContact?.email;
      if (email != null && email.isNotEmpty && state.email.isEmpty) {
        emit(state.copyWith(email: email, suggestedEmail: email));
      }
    } catch (_) {
      // No suggestion — the field just starts empty.
    }
  }

  void setEmail(String value) => emit(state.copyWith(email: value));

  Future<void> send() async {
    if (!state.canSend) return;
    emit(state.copyWith(isSending: true));
    try {
      await _send(request.id, email: state.email.trim());
      if (isClosed) return;
      emit(state.copyWith(isSending: false, actionSeq: state.actionSeq + 1, lastActionSuccess: true));
    } catch (e) {
      if (isClosed) return;
      emit(
        state.copyWith(
          isSending: false,
          actionSeq: state.actionSeq + 1,
          lastActionSuccess: false,
          lastActionFailure: e is CertificateFailure ? e.code : CertificateFailureCode.unknown,
        ),
      );
    }
  }

  @override
  Future<void> close() async {
    await _subscription?.cancel();
    return super.close();
  }
}
