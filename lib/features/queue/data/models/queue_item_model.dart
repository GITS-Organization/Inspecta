import '../../../../core/enums/job_status.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/request_source.dart';

/// A row of `GET /quotation-queue`: an accepted quote in one of its stages.
/// The row names the request it belongs to and carries no line items, so the
/// quote's number stands in as the card's title.
class QueueItemModel {
  final String requestId;
  final String clientName;
  final String? proposalNumber;
  final String location;
  final JobStatus? status;
  final String? inspectorId;
  final String? inspectorName;
  final DateTime? scheduledAt;
  final DateTime? createdAt;
  final DateTime? updatedAt;

  const QueueItemModel({
    required this.requestId,
    required this.clientName,
    required this.location,
    this.proposalNumber,
    this.status,
    this.inspectorId,
    this.inspectorName,
    this.scheduledAt,
    this.createdAt,
    this.updatedAt,
  });

  factory QueueItemModel.fromApiJson(Map<String, dynamic> json) {
    DateTime? date(String key) {
      final value = json[key];
      return value is String ? DateTime.tryParse(value) : null;
    }

    return QueueItemModel(
      requestId: json['requestId'] as String,
      clientName: json['clientName'] as String? ?? '',
      proposalNumber: json['proposalNumber'] as String? ?? json['quotationNumber'] as String?,
      location: json['location'] as String? ?? '',
      status: JobStatus.fromValue(json['requestStatus']),
      inspectorId: json['inspectorId'] as String?,
      inspectorName: json['inspectorName'] as String?,
      scheduledAt: date('scheduledAt'),
      createdAt: date('createdAt'),
      updatedAt: date('updatedAt'),
    );
  }

  /// `null` when the row's request status isn't one the app understands.
  InspectionRequest? toEntity() {
    final resolvedStatus = status;
    if (resolvedStatus == null) return null;
    return InspectionRequest(
      id: requestId,
      source: RequestSource.email,
      clientName: clientName,
      subject: proposalNumber,
      receivedAt: createdAt ?? DateTime.now(),
      location: location,
      status: resolvedStatus,
      items: const [],
      inspectorId: inspectorId,
      inspectorName: inspectorName,
      scheduledAt: scheduledAt,
      updatedAt: updatedAt,
    );
  }
}
