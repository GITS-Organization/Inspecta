import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/domain/machine_certificate_mapper.dart';
import '../../../machines/domain/usecases/get_machine_form.dart';
import '../../../requests/domain/entities/inspection_request.dart';

part 'review_detail_state.dart';

/// Backs one machine's review screen (Feature 06): the inspector's submitted
/// certificate for the machine, read from the machines API. Read-only: the
/// whole job is approved or returned from the summary screen.
///
/// The reviewer's name is their profile name.
class ReviewDetailCubit extends Cubit<ReviewDetailState> {
  final InspectionRequest request;
  final String requestItemId;
  final GetMachineForm _getMachineForm;

  bool _started = false;

  ReviewDetailCubit({
    required this.request,
    required this.requestItemId,
    required String reviewerName,
    required GetMachineForm getMachineForm,
  }) : _getMachineForm = getMachineForm,
       super(ReviewDetailState(reviewerName: reviewerName));

  void start() {
    if (_started) return;
    _started = true;
    _load();
  }

  Future<void> retry() {
    emit(state.copyWith(status: ReviewDetailStatus.loading));
    return _load();
  }

  Future<void> _load() async {
    try {
      final form = await _getMachineForm(request.id, requestItemId);
      if (isClosed) return;
      emit(
        state.copyWith(
          status: ReviewDetailStatus.ready,
          certificate: MachineCertificateMapper.toCertificate(form),
          canReview: form.canReview,
        ),
      );
    } catch (_) {
      if (isClosed) return;
      emit(state.copyWith(status: ReviewDetailStatus.error));
    }
  }
}
