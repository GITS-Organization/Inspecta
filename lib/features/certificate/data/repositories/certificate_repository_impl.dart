import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/enums/job_status.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/domain/repositories/requests_repository.dart';
import '../../domain/entities/certificate.dart';
import '../../domain/entities/certificate_failure.dart';
import '../../domain/repositories/certificate_repository.dart';
import '../datasources/certificate_remote_datasource.dart';
import '../models/certificate_model.dart';

class CertificateRepositoryImpl implements CertificateRepository {
  final CertificateRemoteDataSource _remote;
  final RequestsRepository _requestsRepository;

  const CertificateRepositoryImpl(this._remote, this._requestsRepository);

  @override
  Stream<Certificate?> watchCertificate(String requestId) {
    return _remote
        .watchCertificate(requestId)
        .map((model) => model?.toEntity())
        .handleError((Object error) {
          throw _mapError(error);
        });
  }

  @override
  Stream<List<Certificate>> watchInspectorCertificates(String inspectorId) {
    return _remote
        .watchInspectorCertificates(inspectorId)
        .map((models) => [for (final model in models) model.toEntity()])
        .handleError((Object error) {
          throw _mapError(error);
        });
  }

  @override
  Stream<List<Certificate>> watchReviewedSince(DateTime since) {
    return _remote
        .watchReviewedSince(since)
        .map((models) => [for (final model in models) model.toEntity()])
        .handleError((Object error) {
          throw _mapError(error);
        });
  }

  @override
  Future<void> saveDraft(Certificate certificate) async {
    try {
      await _remote.save(CertificateModel.fromEntity(certificate));
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> submit(Certificate certificate) async {
    try {
      // Submitting (first time, or resubmitting after a return) always
      // clears any prior review note — it was about the version being
      // replaced, not this one.
      final toSubmit = Certificate(
        requestId: certificate.requestId,
        inspectorId: certificate.inspectorId,
        templateId: certificate.templateId,
        texts: certificate.texts,
        dates: certificate.dates,
        answers: certificate.answers,
        functionCheck: certificate.functionCheck,
        finalResult: certificate.finalResult,
      );
      await _remote.submit(CertificateModel.fromEntity(toSubmit));
      await _requestsRepository.markCertificateSubmitted(certificate.requestId);
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> approve(Certificate certificate) async {
    try {
      await _remote.saveReview(CertificateModel.fromEntity(certificate));
      await _requestsRepository.updateStatus(
        certificate.requestId,
        JobStatus.sentToClient,
        note: 'Approved by ${certificate.reviewerName} — signed PDF for ${certificate.sentTo}',
      );
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> sendBack(Certificate certificate, {required String note}) async {
    try {
      final returned = certificate.withReview(reviewNote: note);
      await _remote.saveReview(CertificateModel.fromEntity(returned));
      await _requestsRepository.updateStatus(
        certificate.requestId,
        JobStatus.certificateReturned,
        note: note,
      );
    } catch (e) {
      throw _mapError(e);
    }
  }

  CertificateFailure _mapError(Object error) {
    if (error is CertificateFailure) return error;
    if (error is RequestsFailure) {
      return CertificateFailure(switch (error.code) {
        RequestsFailureCode.permissionDenied => CertificateFailureCode.permissionDenied,
        RequestsFailureCode.network => CertificateFailureCode.network,
        RequestsFailureCode.unknown => CertificateFailureCode.unknown,
      });
    }
    if (error is FirebaseException) {
      return CertificateFailure(switch (error.code) {
        'permission-denied' => CertificateFailureCode.permissionDenied,
        'unavailable' => CertificateFailureCode.network,
        _ => CertificateFailureCode.unknown,
      });
    }
    debugPrint('Unexpected certificate error: $error');
    return const CertificateFailure(CertificateFailureCode.unknown);
  }
}
