import 'package:equatable/equatable.dart';

import 'client_response_outcome.dart';
import 'quote_line_item.dart';
import 'quotation_status.dart';
import 'reply_type.dart';

/// A coordinator's reply to a request (Feature 03 §6) — now including
/// [ReplyType.reject], so the Quotations tracker has a full history of
/// every reply sent, not only priced ones.
///
/// Each *sent* quotation is an immutable version (BR-03.4): sending a new
/// one after this doesn't edit it, it creates another with the next
/// [version].
class Quotation extends Equatable {
  final String id;
  final String requestId;

  /// Denormalized so the quotations list doesn't need to join against
  /// `requests` for every row.
  final String requestNumber;
  final String clientName;

  /// A short summary of what's being quoted, e.g. "Overhead crane × 2".
  /// Empty when the row doesn't carry the equipment (list rows only know how
  /// many [totalUnits]).
  final String equipmentSummary;

  /// Where the inspection is; empty when unknown.
  final String location;

  /// `null` while still a draft; assigned when sent.
  final int? version;
  final ReplyType type;
  final List<QuoteLineItem> items;
  final int? subtotalPiastres;
  final int? totalPiastres;
  final DateTime? validUntil;
  final String? message;

  /// The proposal's "Note that:" list as edited for this quotation; empty
  /// means the defaults (older quotations predate the field).
  final List<String> notes;

  /// The scope of work the coordinator listed for this offer, printed above
  /// the notes, one point each; empty when none was written.
  final List<String> scopeOfWork;
  final QuotationStatus status;
  final DateTime? sentAt;
  final String createdBy;
  final DateTime createdAt;

  /// How many units the quote covers, as the list rows report it. 0 when
  /// unknown (rows that carry [items] don't need it).
  final int totalUnits;

  /// How many of the quote's units still have no price, as the list rows
  /// report it — what a draft has left to fill in.
  final int missingPrices;

  // BR-03.6: the client's answer, logged by the coordinator.
  final ClientResponseOutcome? clientResponseOutcome;
  final int? clientCounterPricePiastres;
  final String? clientResponseNote;
  final DateTime? clientRespondedAt;

  /// Last time "Send reminder" was used (the email itself isn't sent by
  /// this app — see the quotation feature's known gaps).
  final DateTime? lastReminderAt;

  const Quotation({
    required this.id,
    required this.requestId,
    required this.requestNumber,
    required this.clientName,
    required this.equipmentSummary,
    required this.type,
    required this.items,
    required this.status,
    required this.createdBy,
    required this.createdAt,
    this.version,
    this.location = '',
    this.totalUnits = 0,
    this.missingPrices = 0,
    this.subtotalPiastres,
    this.totalPiastres,
    this.validUntil,
    this.message,
    this.notes = const [],
    this.scopeOfWork = const [],
    this.sentAt,
    this.clientResponseOutcome,
    this.clientCounterPricePiastres,
    this.clientResponseNote,
    this.clientRespondedAt,
    this.lastReminderAt,
  });

  bool get isDraft => status == QuotationStatus.draft;

  /// BR-03.3 / acceptance criterion 4: past its validity, an offer can no
  /// longer be accepted without a new version.
  bool isExpired({DateTime? now}) {
    final effectiveNow = now ?? DateTime.now();
    return (status == QuotationStatus.sent || status == QuotationStatus.countered) &&
        validUntil != null &&
        validUntil!.isBefore(effectiveNow);
  }

  /// Amber-zone: still open, but due within 2 days.
  bool isExpiringSoon({DateTime? now}) {
    if (isExpired(now: now)) return false;
    if (status != QuotationStatus.sent && status != QuotationStatus.countered) return false;
    if (validUntil == null) return false;
    return validUntil!.difference(now ?? DateTime.now()) <= const Duration(days: 2);
  }

  Duration? waitingFor({DateTime? now}) =>
      sentAt == null ? null : (now ?? DateTime.now()).difference(sentAt!);

  @override
  List<Object?> get props => [
    id,
    requestId,
    requestNumber,
    clientName,
    equipmentSummary,
    location,
    version,
    type,
    items,
    subtotalPiastres,
    totalPiastres,
    validUntil,
    message,
    notes,
    scopeOfWork,
    status,
    sentAt,
    createdBy,
    createdAt,
    totalUnits,
    missingPrices,
    clientResponseOutcome,
    clientCounterPricePiastres,
    clientResponseNote,
    clientRespondedAt,
    lastReminderAt,
  ];
}
