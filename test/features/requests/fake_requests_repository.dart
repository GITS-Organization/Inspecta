import 'dart:async';

import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/requests/domain/entities/inspection_request.dart';
import 'package:inspecta/features/requests/domain/entities/request_item.dart';
import 'package:inspecta/features/requests/domain/entities/request_source.dart';
import 'package:inspecta/features/requests/domain/entities/requests_failure.dart';
import 'package:inspecta/features/requests/domain/repositories/requests_repository.dart';

InspectionRequest sampleRequest({
  String id = 'REQ-2026-0001',
  JobStatus status = JobStatus.requestReceived,
  String clientName = 'Delta Steel Co.',
  String? clientId = 'client-1',
  DateTime? receivedAt,
  bool hasUnreadClientReply = false,
  List<RequestItem>? items,
  DateTime? scheduledAt,
  DateTime? certificateSubmittedAt,
}) {
  return InspectionRequest(
    id: id,
    source: RequestSource.email,
    clientId: clientId,
    clientName: clientName,
    receivedAt: receivedAt ?? DateTime.now(),
    location: '10th of Ramadan',
    status: status,
    items:
        items ??
        const [
          RequestItem(
            id: 'item-1',
            type: 'Overhead crane',
            capacity: '10 t',
            quantity: 2,
          ),
        ],
    hasUnreadClientReply: hasUnreadClientReply,
    scheduledAt: scheduledAt,
    certificateSubmittedAt: certificateSubmittedAt,
  );
}

class FakeRequestsRepository implements RequestsRepository {
  final controller = StreamController<List<InspectionRequest>>.broadcast();

  /// What `watchAssignedRequests` (an inspector's own jobs) emits.
  final assignedController = StreamController<List<InspectionRequest>>.broadcast();

  InspectionRequest? requestToReturn;
  RequestsFailure? rejectFailure;
  final statusUpdates = <(String requestId, JobStatus status, String? note)>[];
  final rejections = <(String requestId, String reason)>[];
  final clientAssignments = <(String requestId, String clientId)>[];
  final inspectorAssignments =
      <(String requestId, String inspectorId, DateTime scheduledAt, String? note)>[];

  @override
  Stream<List<InspectionRequest>> watchRequests() => controller.stream;

  @override
  Future<InspectionRequest?> getById(String id) async => requestToReturn;

  @override
  Future<void> updateStatus(
    String requestId,
    JobStatus status, {
    String? note,
  }) async {
    statusUpdates.add((requestId, status, note));
  }

  @override
  Future<void> markCertificateSubmitted(String requestId) async {
    statusUpdates.add((requestId, JobStatus.certificateSubmitted, null));
  }

  @override
  Future<void> rejectRequest({
    required String requestId,
    required String reason,
  }) async {
    if (rejectFailure != null) throw rejectFailure!;
    rejections.add((requestId, reason));
  }

  @override
  Future<void> assignClient(String requestId, String clientId) async {
    clientAssignments.add((requestId, clientId));
  }

  @override
  Future<void> assignInspector({
    required String requestId,
    required String inspectorId,
    required DateTime scheduledAt,
    String? note,
  }) async {
    inspectorAssignments.add((requestId, inspectorId, scheduledAt, note));
  }

  @override
  Future<void> completeIntake({
    required String requestId,
    required String location,
    required List<RequestItem> items,
  }) {
    // TODO: implement completeIntake
    throw UnimplementedError();
  }

  @override
  Future<void> declineAssignment({required String requestId, String? reason}) {
    // TODO: implement declineAssignment
    throw UnimplementedError();
  }

  @override
  Stream<List<InspectionRequest>> watchAssignedRequests(String inspectorId) => assignedController.stream;
}
