import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/enums/job_status.dart';
import '../../domain/entities/inspection_request.dart';
import '../../domain/entities/request_item.dart';
import '../../domain/entities/requests_failure.dart';
import '../../domain/repositories/requests_repository.dart';
import '../datasources/requests_remote_datasource.dart';

class RequestsRepositoryImpl implements RequestsRepository {
  final RequestsRemoteDataSource _remote;

  const RequestsRepositoryImpl(this._remote);

  @override
  Stream<List<InspectionRequest>> watchRequests() {
    return _remote
        .watchRequests()
        .map((models) {
          final requests = <InspectionRequest>[];
          for (final model in models) {
            final entity = model.toEntity();
            if (entity != null) {
              requests.add(entity);
            } else {
              debugPrint('Skipping requests/${model.id}: unknown status');
            }
          }
          return requests;
        })
        .handleError((Object error) {
          throw _mapError(error);
        });
  }

  @override
  Stream<List<InspectionRequest>> watchAssignedRequests(String inspectorId) {
    return _remote
        .watchAssignedRequests(inspectorId)
        .map((models) {
          final requests = <InspectionRequest>[];
          for (final model in models) {
            final entity = model.toEntity();
            if (entity != null) {
              requests.add(entity);
            } else {
              debugPrint('Skipping requests/${model.id}: unknown status');
            }
          }
          return requests;
        })
        .handleError((Object error) {
          throw _mapError(error);
        });
  }

  @override
  Future<InspectionRequest?> getById(String id) async {
    try {
      final model = await _remote.getById(id);
      return model?.toEntity();
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> updateStatus(
    String requestId,
    JobStatus status, {
    String? note,
  }) async {
    try {
      await _remote.updateStatus(requestId, status, note: note);
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> markCertificateSubmitted(String requestId) async {
    try {
      await _remote.markCertificateSubmitted(requestId);
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> rejectRequest({
    required String requestId,
    required String reason,
  }) async {
    try {
      await _remote.updateStatus(
        requestId,
        JobStatus.quoteRejected,
        note: reason,
        extra: {'rejectReason': reason},
      );
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> assignClient(String requestId, String clientId) async {
    try {
      await _remote.assignClient(requestId, clientId);
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> assignInspector({
    required String requestId,
    required String inspectorId,
    required DateTime scheduledAt,
    String? note,
  }) async {
    try {
      await _remote.assignInspector(
        requestId,
        inspectorId: inspectorId,
        scheduledAt: scheduledAt,
        note: note,
      );
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> declineAssignment({required String requestId, String? reason}) async {
    try {
      await _remote.declineAssignment(requestId, reason: reason);
    } catch (e) {
      throw _mapError(e);
    }
  }

  @override
  Future<void> completeIntake({
    required String requestId,
    required String location,
    required List<RequestItem> items,
  }) async {
    try {
      await _remote.completeIntake(
        requestId,
        location: location,
        items: items
            .map(
              (item) => {
                'id': item.id,
                'type': item.type,
                'category': item.category,
                'manufacturer': item.manufacturer,
                'model': item.model,
                'serialNumber': item.serialNumber,
                'capacity': item.capacity,
                'quantity': item.quantity,
              },
            )
            .toList(),
      );
    } catch (e) {
      throw _mapError(e);
    }
  }

  RequestsFailure _mapError(Object error) {
    if (error is RequestsFailure) return error;
    if (error is FirebaseException) {
      return RequestsFailure(switch (error.code) {
        'permission-denied' => RequestsFailureCode.permissionDenied,
        'unavailable' => RequestsFailureCode.network,
        _ => RequestsFailureCode.unknown,
      });
    }
    debugPrint('Unexpected requests error: $error');
    return const RequestsFailure(RequestsFailureCode.unknown);
  }
}
