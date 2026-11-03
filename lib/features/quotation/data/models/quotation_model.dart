import '../../../../core/utils/currency.dart';
import '../../../../core/enums/job_status.dart';
import '../../domain/entities/client_reply.dart';
import '../../domain/entities/client_response_outcome.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_status.dart';
import '../../domain/entities/reply_type.dart';
import 'quote_line_item_model.dart';

/// A quote as the Inspecta API returns it, in full or as a list row.
class QuotationModel {
  final String id;
  final String requestId;
  final String requestNumber;
  final String clientName;
  final String equipmentSummary;
  final String location;
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
  final int totalUnits;
  final int missingPrices;

  /// ISO code of the currency the amounts are in.
  final String currency;
  final String? clientResponseOutcome;
  final int? clientCounterPricePiastres;
  final String? clientResponseNote;
  final DateTime? clientRespondedAt;
  final DateTime? lastReminderAt;
  final List<ClientReply> clientReplies;
  final bool hasUnreadClientReply;
  final String? requestStatus;

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
    this.location = '',
    this.totalUnits = 0,
    this.missingPrices = 0,
    this.currency = Currency.egp,
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
    this.clientReplies = const [],
    this.hasUnreadClientReply = false,
    this.requestStatus,
  });

  /// The client's emails on a quote. The `html` of each is deliberately not
  /// read: it is the email as it arrived and can't be trusted.
  static List<ClientReply> _replies(Object? value) {
    if (value is! List) return const [];
    return [
      for (final json in value.whereType<Map<String, dynamic>>())
        ClientReply(
          id: json['id'] as String? ?? '',
          quoteId: json['quoteId'] as String? ?? '',
          fromAddress: json['fromAddress'] as String? ?? '',
          fromName: json['fromName'] as String? ?? '',
          subject: json['subject'] as String? ?? '',
          receivedAt: DateTime.tryParse(json['receivedAt'] as String? ?? ''),
          text: (json['text'] as String? ?? '').trim(),
          attachments: [
            for (final file in (json['attachments'] as List<dynamic>? ?? const []).whereType<Map<String, dynamic>>())
              ReplyAttachment(
                fileName: file['fileName'] as String? ?? '',
                contentType: file['contentType'] as String? ?? '',
                sizeBytes: (file['size'] as num?)?.toInt() ?? 0,
              ),
          ],
        ),
    ];
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
      currency: Currency.normalize(json['currency'] as String?),
      location: json['location'] as String? ?? '',
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
          : (status == 'rejected' ? QuotationStatus.declined.value : status),
      sentAt: sentAt,
      createdBy: '',
      createdAt: date('createdAt') ?? DateTime.now(),
      clientResponseNote: json['clientReply'] as String?,
      clientReplies: _replies(json['clientReplies']),
      hasUnreadClientReply: json['hasUnreadClientReply'] == true,
      requestStatus: json['requestStatus'] as String?,
    );
  }

  /// A row of `GET /quotations`: the summary a list needs, with no line
  /// items or notes. `stage` says whether the quote was ever sent, so an
  /// `open` quote is a draft until then. The row has no equipment names, only
  /// how many units, so the equipment summary is left empty.
  factory QuotationModel.fromApiListJson(Map<String, dynamic> json) {
    DateTime? date(String key) {
      final value = json[key];
      return value is String ? DateTime.tryParse(value) : null;
    }

    final status = json['status'] as String?;
    final amount = (json['amountPiastres'] as num?)?.toInt();

    return QuotationModel(
      id: json['id'] as String,
      requestId: json['requestId'] as String? ?? '',
      requestNumber:
          json['proposalNumber'] as String? ?? json['quotationNumber'] as String? ?? '',
      clientName: json['clientName'] as String? ?? '',
      equipmentSummary: '',
      currency: Currency.normalize(json['currency'] as String?),
      location: json['location'] as String? ?? '',
      version: (json['version'] as num?)?.toInt(),
      totalUnits: (json['totalUnits'] as num?)?.toInt() ?? 0,
      missingPrices: (json['missingPrices'] as num?)?.toInt() ?? 0,
      type: ReplyType.offer.value,
      items: const [],
      subtotalPiastres: amount,
      totalPiastres: amount,
      validUntil: date('validUntil'),
      status: status == 'open'
          ? (json['stage'] == 'draft'
                ? QuotationStatus.draft.value
                : QuotationStatus.sent.value)
          : (status == 'rejected' ? QuotationStatus.declined.value : status),
      sentAt: date('sentAt'),
      createdBy: '',
      createdAt: date('createdAt') ?? DateTime.now(),
      hasUnreadClientReply: json['hasUnreadClientReply'] == true,
      requestStatus: json['requestStatus'] as String?,
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
      location: location,
      version: version,
      totalUnits: totalUnits,
      missingPrices: missingPrices,
      currency: currency,
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
      clientReplies: clientReplies,
      hasUnreadClientReply: hasUnreadClientReply,
      requestStatus: JobStatus.fromValue(requestStatus),
    );
  }
}
