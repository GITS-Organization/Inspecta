import 'package:cloud_firestore/cloud_firestore.dart';

import '../../../../core/enums/job_status.dart';
import '../../domain/entities/inspection_request.dart';
import '../../domain/entities/request_source.dart';
import 'request_item_model.dart';

/// A `requests/{id}` Firestore document.
class InspectionRequestModel {
  final String id;
  final RequestSource source;
  final String? clientId;
  final String clientName;
  final String? subject;
  final String? emailFrom;
  final String? emailFromName;
  final String? emailPreview;
  final DateTime receivedAt;
  final String location;
  final DateTime? preferredDate;
  final String? accessNotes;
  final JobStatus? status;
  final String? coordinatorId;
  final List<RequestItemModel> items;
  final String? rejectReason;
  final String? inspectorId;
  final DateTime? scheduledAt;
  final String? assignmentNote;
  final DateTime? certificateSubmittedAt;

  /// When the API last changed the request: sent back on an update so the
  /// API can refuse it if someone else changed the request meanwhile.
  final DateTime? updatedAt;

  const InspectionRequestModel({
    required this.id,
    required this.source,
    required this.clientName,
    required this.receivedAt,
    required this.location,
    required this.items,
    this.clientId,
    this.subject,
    this.emailFrom,
    this.emailFromName,
    this.emailPreview,
    this.preferredDate,
    this.accessNotes,
    this.status,
    this.coordinatorId,
    this.rejectReason,
    this.inspectorId,
    this.scheduledAt,
    this.assignmentNote,
    this.certificateSubmittedAt,
    this.updatedAt,
  });

  /// A request as the Inspecta API returns it: ISO-8601 date strings instead
  /// of Firestore timestamps, and the id inside the object.
  factory InspectionRequestModel.fromApiJson(Map<String, dynamic> json) {
    DateTime? date(String key) {
      final value = json[key];
      return value is String ? DateTime.tryParse(value) : null;
    }

    return InspectionRequestModel(
      id: json['id'] as String,
      source: RequestSource.fromValue(json['source']),
      clientId: json['clientId'] as String?,
      clientName: json['clientName'] as String? ?? '',
      subject: json['subject'] as String?,
      emailFrom: json['emailFrom'] as String?,
      emailFromName: json['emailFromName'] as String?,
      emailPreview: json['emailPreview'] as String?,
      receivedAt: date('receivedAt') ?? DateTime.now(),
      location: json['location'] as String? ?? '',
      preferredDate: date('preferredDate'),
      accessNotes: json['accessNotes'] as String?,
      status: JobStatus.fromValue(json['status']),
      coordinatorId: json['coordinatorId'] as String?,
      items: (json['items'] as List<dynamic>? ?? const [])
          .whereType<Map<String, dynamic>>()
          .map(RequestItemModel.fromJson)
          .toList(),
      inspectorId: json['inspectorId'] as String?,
      scheduledAt: date('scheduledAt'),
      updatedAt: date('updatedAt'),
    );
  }

  factory InspectionRequestModel.fromJson(String id, Map<String, dynamic> json) {
    return InspectionRequestModel(
      id: id,
      source: RequestSource.fromValue(json['source']),
      clientId: json['clientId'] as String?,
      clientName: json['clientName'] as String? ?? '',
      subject: json['subject'] as String?,
      emailFrom: json['emailFrom'] as String?,
      emailFromName: json['emailFromName'] as String?,
      emailPreview: json['emailPreview'] as String?,
      receivedAt:
          (json['receivedAt'] as Timestamp?)?.toDate() ?? DateTime.now(),
      location: json['location'] as String? ?? '',
      preferredDate: (json['preferredDate'] as Timestamp?)?.toDate(),
      accessNotes: json['accessNotes'] as String?,
      status: JobStatus.fromValue(json['status']),
      // Older documents stored this as `supervisorId`.
      coordinatorId: (json['coordinatorId'] ?? json['supervisorId']) as String?,
      items: (json['items'] as List<dynamic>? ?? const [])
          .whereType<Map<String, dynamic>>()
          .map(RequestItemModel.fromJson)
          .toList(),
      rejectReason: json['rejectReason'] as String?,
      inspectorId: json['inspectorId'] as String?,
      scheduledAt: (json['scheduledAt'] as Timestamp?)?.toDate(),
      assignmentNote: json['assignmentNote'] as String?,
      certificateSubmittedAt: (json['certificateSubmittedAt'] as Timestamp?)?.toDate(),
    );
  }

  /// `null` when [status] isn't a value the app understands, so a request
  /// with a bad/future status doesn't show up mis-filed in a tab.
  InspectionRequest? toEntity() {
    final resolvedStatus = status;
    if (resolvedStatus == null) return null;
    return InspectionRequest(
      id: id,
      source: source,
      clientId: clientId,
      clientName: clientName,
      subject: subject,
      emailFrom: emailFrom,
      emailFromName: emailFromName,
      emailPreview: emailPreview,
      receivedAt: receivedAt,
      location: location,
      preferredDate: preferredDate,
      accessNotes: accessNotes,
      status: resolvedStatus,
      coordinatorId: coordinatorId,
      items: items.map((item) => item.toEntity()).toList(),
      rejectReason: rejectReason,
      inspectorId: inspectorId,
      scheduledAt: scheduledAt,
      assignmentNote: assignmentNote,
      certificateSubmittedAt: certificateSubmittedAt,
      updatedAt: updatedAt,
    );
  }
}
