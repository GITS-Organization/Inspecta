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
  final String? emailThreadId;
  final String? subject;
  final String? emailFrom;
  final String? emailPreview;
  final DateTime receivedAt;
  final String location;
  final DateTime? preferredDate;
  final String? accessNotes;
  final JobStatus? status;
  final String? coordinatorId;
  final List<RequestItemModel> items;
  final bool hasUnreadClientReply;
  final String? lastReplySnippet;
  final String? rejectReason;
  final String? inspectorId;
  final DateTime? scheduledAt;
  final String? assignmentNote;
  final DateTime? certificateSubmittedAt;

  const InspectionRequestModel({
    required this.id,
    required this.source,
    required this.clientName,
    required this.receivedAt,
    required this.location,
    required this.items,
    this.clientId,
    this.emailThreadId,
    this.subject,
    this.emailFrom,
    this.emailPreview,
    this.preferredDate,
    this.accessNotes,
    this.status,
    this.coordinatorId,
    this.hasUnreadClientReply = false,
    this.lastReplySnippet,
    this.rejectReason,
    this.inspectorId,
    this.scheduledAt,
    this.assignmentNote,
    this.certificateSubmittedAt,
  });

  factory InspectionRequestModel.fromJson(String id, Map<String, dynamic> json) {
    return InspectionRequestModel(
      id: id,
      source: RequestSource.fromValue(json['source']),
      clientId: json['clientId'] as String?,
      clientName: json['clientName'] as String? ?? '',
      emailThreadId: json['emailThreadId'] as String?,
      subject: json['subject'] as String?,
      emailFrom: json['emailFrom'] as String?,
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
      hasUnreadClientReply: json['hasUnreadClientReply'] as bool? ?? false,
      lastReplySnippet: json['lastReplySnippet'] as String?,
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
      emailThreadId: emailThreadId,
      subject: subject,
      emailFrom: emailFrom,
      emailPreview: emailPreview,
      receivedAt: receivedAt,
      location: location,
      preferredDate: preferredDate,
      accessNotes: accessNotes,
      status: resolvedStatus,
      coordinatorId: coordinatorId,
      items: items.map((item) => item.toEntity()).toList(),
      hasUnreadClientReply: hasUnreadClientReply,
      lastReplySnippet: lastReplySnippet,
      rejectReason: rejectReason,
      inspectorId: inspectorId,
      scheduledAt: scheduledAt,
      assignmentNote: assignmentNote,
      certificateSubmittedAt: certificateSubmittedAt,
    );
  }
}
