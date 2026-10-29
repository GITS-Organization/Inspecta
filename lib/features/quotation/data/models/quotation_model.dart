import 'package:cloud_firestore/cloud_firestore.dart';

import '../../domain/entities/client_response_outcome.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_status.dart';
import '../../domain/entities/reply_type.dart';
import 'quote_line_item_model.dart';

/// A `quotations/{id}` Firestore document.
class QuotationModel {
  final String id;
  final String requestId;
  final String requestNumber;
  final String clientName;
  final String equipmentSummary;
  final int? version;
  final String? type;
  final List<QuoteLineItemModel> items;
  final int? subtotalPiastres;
  final int? totalPiastres;
  final DateTime? validUntil;
  final String? message;
  final List<String> notes;
  final List<String> scopeOfWork;
  final String? status;
  final DateTime? sentAt;
  final String createdBy;
  final DateTime createdAt;
  final String? clientResponseOutcome;
  final int? clientCounterPricePiastres;
  final String? clientResponseNote;
  final DateTime? clientRespondedAt;
  final DateTime? lastReminderAt;

  const QuotationModel({
    required this.id,
    required this.requestId,
    required this.requestNumber,
    required this.clientName,
    required this.equipmentSummary,
    required this.items,
    required this.createdBy,
    required this.createdAt,
    this.version,
    this.type,
    this.subtotalPiastres,
    this.totalPiastres,
    this.validUntil,
    this.message,
    this.notes = const [],
    this.scopeOfWork = const [],
    this.status,
    this.sentAt,
    this.clientResponseOutcome,
    this.clientCounterPricePiastres,
    this.clientResponseNote,
    this.clientRespondedAt,
    this.lastReminderAt,
  });

  factory QuotationModel.fromJson(String id, Map<String, dynamic> json) {
    return QuotationModel(
      id: id,
      requestId: json['requestId'] as String? ?? '',
      requestNumber: json['requestNumber'] as String? ?? '',
      clientName: json['clientName'] as String? ?? '',
      equipmentSummary: json['equipmentSummary'] as String? ?? '',
      version: (json['version'] as num?)?.toInt(),
      type: json['type'] as String?,
      items: (json['items'] as List<dynamic>? ?? const [])
          .whereType<Map<String, dynamic>>()
          .map(QuoteLineItemModel.fromJson)
          .toList(),
      subtotalPiastres: (json['subtotalPiastres'] as num?)?.toInt(),
      totalPiastres: (json['totalPiastres'] as num?)?.toInt(),
      validUntil: (json['validUntil'] as Timestamp?)?.toDate(),
      message: json['message'] as String?,
      notes: (json['notes'] as List<dynamic>? ?? const []).whereType<String>().toList(),
      scopeOfWork: (json['scopeOfWork'] as List<dynamic>? ?? const []).whereType<String>().toList(),
      status: json['status'] as String?,
      sentAt: (json['sentAt'] as Timestamp?)?.toDate(),
      createdBy: json['createdBy'] as String? ?? '',
      createdAt: (json['createdAt'] as Timestamp?)?.toDate() ?? DateTime.now(),
      clientResponseOutcome: json['clientResponseOutcome'] as String?,
      clientCounterPricePiastres: (json['clientCounterPricePiastres'] as num?)?.toInt(),
      clientResponseNote: json['clientResponseNote'] as String?,
      clientRespondedAt: (json['clientRespondedAt'] as Timestamp?)?.toDate(),
      lastReminderAt: (json['lastReminderAt'] as Timestamp?)?.toDate(),
    );
  }

  /// A quote as the Inspecta API returns it. The API has no reply type (every
  /// quote is a priced offer), calls the number `proposalNumber`, and calls a
  /// sent quote that awaits an answer `open`.
  factory QuotationModel.fromApiJson(Map<String, dynamic> json) {
    DateTime? date(String key) {
      final value = json[key];
      return value is String ? DateTime.tryParse(value) : null;
    }

    List<String> strings(String key) =>
        (json[key] as List<dynamic>? ?? const []).whereType<String>().toList();

    final items = (json['items'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(QuoteLineItemModel.fromApiJson)
        .toList();
    final sentAt = date('sentAt');
    final status = json['status'] as String?;
    final amount = (json['amountPiastres'] as num?)?.toInt();

    return QuotationModel(
      id: json['id'] as String,
      requestId: json['requestId'] as String? ?? '',
      requestNumber: json['proposalNumber'] as String? ?? '',
      clientName: json['clientName'] as String? ?? '',
      equipmentSummary: _summarise(items),
      version: (json['version'] as num?)?.toInt(),
      type: ReplyType.offer.value,
      items: items,
      subtotalPiastres: amount,
      totalPiastres: amount,
      validUntil: date('validUntil'),
      message: json['message'] as String?,
      notes: strings('notes'),
      scopeOfWork: strings('scopeOfWork'),
      status: status == 'open'
          ? (sentAt == null ? QuotationStatus.draft.value : QuotationStatus.sent.value)
          : status,
      sentAt: sentAt,
      createdBy: '',
      createdAt: date('createdAt') ?? DateTime.now(),
      clientResponseNote: json['clientReply'] as String?,
    );
  }

  /// "Overhead crane × 2", or with a second line "Overhead crane × 2, Hoist
  /// × 1" — what a list row says the quote is for.
  static String _summarise(List<QuoteLineItemModel> items) {
    final lines = [
      for (final item in items.take(2))
        item.quantity > 1 ? '${item.type} × ${item.quantity}' : item.type,
    ];
    final more = items.length - lines.length;
    return [...lines, if (more > 0) '+$more'].join(', ');
  }

  /// `null` when [type] or [status] isn't a value the app understands.
  Quotation? toEntity() {
    final replyType = ReplyType.fromValue(type);
    final quotationStatus = QuotationStatus.fromValue(status);
    if (replyType == null || quotationStatus == null) return null;
    return Quotation(
      id: id,
      requestId: requestId,
      requestNumber: requestNumber,
      clientName: clientName,
      equipmentSummary: equipmentSummary,
      version: version,
      type: replyType,
      items: items.map((item) => item.toEntity()).toList(),
      subtotalPiastres: subtotalPiastres,
      totalPiastres: totalPiastres,
      validUntil: validUntil,
      message: message,
      notes: notes,
      scopeOfWork: scopeOfWork,
      status: quotationStatus,
      sentAt: sentAt,
      createdBy: createdBy,
      createdAt: createdAt,
      clientResponseOutcome: ClientResponseOutcome.fromValue(clientResponseOutcome),
      clientCounterPricePiastres: clientCounterPricePiastres,
      clientResponseNote: clientResponseNote,
      clientRespondedAt: clientRespondedAt,
      lastReminderAt: lastReminderAt,
    );
  }
}
