import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../certificate/domain/certificate_number.dart';
import '../../../certificate/domain/certificate_verification.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/domain/entities/certificate_failure.dart';
import '../../../certificate/domain/usecases/approve_certificate.dart';
import '../../../certificate/domain/usecases/return_certificate.dart';
import '../../../certificate/domain/usecases/watch_certificate.dart';
import '../../../certificate/presentation/pdf/certificate_pdf_builder.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../domain/usecases/save_signature.dart';
import '../../domain/usecases/watch_signature.dart';

part 'review_detail_state.dart';

/// Backs one certificate's review screen (Feature 06): the inspector's
/// submitted certificate, plus the reviewer's own comments and signature —
/// approved with both (which also issues its QR code, and hands it to the
/// coordinator to send to the client), or returned with a reason.
///
/// The reviewer's name is their profile name, and their signature is the one
/// saved in their profile when they have one — they only draw it here, once,
/// if they haven't.
class ReviewDetailCubit extends Cubit<ReviewDetailState> {
  final InspectionRequest request;
  final String managerId;
  final WatchCertificate _watchCertificate;
  final ApproveCertificate _approve;
  final ReturnCertificate _return;
  final WatchSignature _watchSignature;
  final SaveSignature _saveSignature;

  StreamSubscription<Certificate?>? _subscription;
  StreamSubscription<List<List<double>>>? _signatureSubscription;

  ReviewDetailCubit({
    required this.request,
    required this.managerId,
    required String reviewerName,
    required WatchCertificate watchCertificate,
    required ApproveCertificate approveCertificate,
    required ReturnCertificate returnCertificate,
    required WatchSignature watchSignature,
    required SaveSignature saveSignature,
  }) : _watchCertificate = watchCertificate,
       _approve = approveCertificate,
       _return = returnCertificate,
       _watchSignature = watchSignature,
       _saveSignature = saveSignature,
       super(ReviewDetailState(reviewerName: reviewerName));

  void start() {
    if (_subscription != null) return;
    _watchSavedSignature();
    _subscription = _watchCertificate(request.id).listen(
      (certificate) => emit(
        state.copyWith(
          status: certificate == null ? ReviewDetailStatus.notFound : ReviewDetailStatus.ready,
          certificate: certificate,
        ),
      ),
      onError: (Object error) => emit(state.copyWith(status: ReviewDetailStatus.error)),
    );
  }

  void _watchSavedSignature() {
    if (managerId.isEmpty) {
      emit(state.copyWith(savedStrokes: const []));
      return;
    }
    _signatureSubscription = _watchSignature(managerId).listen(
      (strokes) => emit(state.copyWith(savedStrokes: strokes)),
      // Can't be read: the reviewer just draws one, as if none were saved.
      onError: (_) => emit(state.copyWith(savedStrokes: const [])),
    );
  }

  void setComments(String value) => emit(state.copyWith(comments: value));

  void setStrokes(List<List<double>> strokes) => emit(state.copyWith(strokes: strokes));

  void clearSignature() => emit(state.copyWith(strokes: const []));

  void setRememberSignature(bool value) => emit(state.copyWith(rememberSignature: value));

  Future<void> approve() async {
    final certificate = state.certificate;
    if (certificate == null || !state.canApprove || state.isBusy) return;
    emit(state.copyWith(isApproving: true));
    final comments = state.comments.trim();
    try {
      // The QR is issued here, at the moment of approval, and stored on the
      // certificate: the PDF and the dashboard both read the same link back,
      // and a client who scans it lands on the dashboard's public page.
      final code = newVerificationCode();
      await _approve(
        certificate.withReview(
          reviewerComments: comments.isEmpty ? null : comments,
          reviewerName: state.reviewerName.trim(),
          signatureStrokes: state.signatureStrokes,
          certificateNumber: certNumberFor(request),
          verificationCode: code,
          qrData: certificateQrData(request: request, verificationCode: code),
          issuedFor: certificateIssuedFor(request: request, certificate: certificate),
        ),
      );
      // Drawn here, and they asked to keep it: it's saved to their profile so
      // they aren't asked again. The approval already went through, so a
      // failure to save it isn't worth reporting.
      if (!state.hasSavedSignature && state.rememberSignature) {
        unawaited(_saveSignature(managerId, state.strokes).catchError((Object _) {}));
      }
      _finish(approved: true);
    } catch (e) {
      _fail(e);
    }
  }

  Future<void> sendBack(String note) async {
    final certificate = state.certificate;
    if (certificate == null || state.isBusy) return;
    emit(state.copyWith(isReturning: true));
    try {
      await _return(certificate, note: note.trim());
      _finish(approved: false);
    } catch (e) {
      _fail(e);
    }
  }

  void _finish({required bool approved}) {
    if (isClosed) return;
    emit(
      state.copyWith(
        isApproving: false,
        isReturning: false,
        actionSeq: state.actionSeq + 1,
        lastActionSuccess: true,
        lastActionApproved: approved,
      ),
    );
  }

  void _fail(Object error) {
    if (isClosed) return;
    emit(
      state.copyWith(
        isApproving: false,
        isReturning: false,
        actionSeq: state.actionSeq + 1,
        lastActionSuccess: false,
        lastActionFailure: error is CertificateFailure ? error.code : CertificateFailureCode.unknown,
      ),
    );
  }

  @override
  Future<void> close() async {
    await _subscription?.cancel();
    await _signatureSubscription?.cancel();
    return super.close();
  }
}
