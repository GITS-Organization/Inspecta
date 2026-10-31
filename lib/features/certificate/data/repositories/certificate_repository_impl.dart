import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/enums/job_status.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/domain/repositories/requests_repository.dart';
import '../../domain/entities/certificate.dart';
import '../../domain/entities/certificate_failure.dart';
import '../../domain/repositories/certificate_repository.dart';
import '../datasources/certificate_remote_datasource.dart';

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
  Future<void> markSentToClient(String requestId, {required String email}) async {
    try {
      await _remote.saveSentTo(requestId, email);
      await _requestsRepository.updateStatus(
        requestId,
        JobStatus.sentToClient,
        note: 'Signed PDF sent to $email',
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
        RequestsFailureCode.conflict || RequestsFailureCode.unknown => CertificateFailureCode.unknown,
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
