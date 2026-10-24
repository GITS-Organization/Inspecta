import 'package:inspecta/features/quotation/domain/entities/client_response_outcome.dart';
import 'package:inspecta/features/quotation/domain/entities/quotation.dart';
import 'package:inspecta/features/quotation/domain/entities/quotation_failure.dart';
import 'package:inspecta/features/quotation/domain/entities/quotation_status.dart';
import 'package:inspecta/features/quotation/domain/entities/quote_line_item.dart';
import 'package:inspecta/features/quotation/domain/entities/reply_type.dart';
import 'package:inspecta/features/quotation/domain/repositories/quotation_repository.dart';

class FakeQuotationRepository implements QuotationRepository {
  QuotationFailure? saveDraftFailure;
  QuotationFailure? sendFailure;
  QuotationFailure? recordResponseFailure;
  int nextVersion = 1;

  final saveDraftCalls = <({ReplyType type, List<QuoteLineItem> items, String? message, List<String>? scopeOfWork})>[];
  final sendCalls = <({ReplyType type, List<QuoteLineItem> items, String? message, List<String>? scopeOfWork})>[];
  final responseCalls =
      <({String quotationId, ClientResponseOutcome outcome, int? clientPrice})>[];
  final reminderCalls = <String>[];

  @override
  Stream<List<Quotation>> watchAll() => const Stream.empty();

  @override
  Stream<List<Quotation>> watchForRequest(String requestId) => const Stream.empty();

  @override
  Future<Quotation> saveDraft({
    required String requestId,
    required String requestNumber,
    required String clientName,
    required String equipmentSummary,
    required ReplyType type,
    required List<QuoteLineItem> items,
    int? validityDays,
    String? message,
    List<String>? notes,
    List<String>? scopeOfWork,
  }) async {
    saveDraftCalls.add((type: type, items: items, message: message, scopeOfWork: scopeOfWork));
    if (saveDraftFailure != null) throw saveDraftFailure!;
    return Quotation(
      id: 'q1',
      requestId: requestId,
      requestNumber: requestNumber,
      clientName: clientName,
      equipmentSummary: equipmentSummary,
      type: type,
      items: items,
      status: QuotationStatus.draft,
      createdBy: 'u1',
      createdAt: DateTime.now(),
    );
  }

  @override
  Future<int> send({
    required String requestId,
    required String requestNumber,
    required String clientName,
    required String equipmentSummary,
    required ReplyType type,
    required List<QuoteLineItem> items,
    int? validityDays,
    String? message,
    List<String>? notes,
    List<String>? scopeOfWork,
  }) async {
    sendCalls.add((type: type, items: items, message: message, scopeOfWork: scopeOfWork));
    if (sendFailure != null) throw sendFailure!;
    return nextVersion;
  }

  @override
  Future<void> recordClientResponse({
    required String quotationId,
    required String requestId,
    required ClientResponseOutcome outcome,
    int? clientPricePiastres,
    String? note,
  }) async {
    responseCalls.add((quotationId: quotationId, outcome: outcome, clientPrice: clientPricePiastres));
    if (recordResponseFailure != null) throw recordResponseFailure!;
  }

  @override
  Future<void> sendReminder(String quotationId) async {
    reminderCalls.add(quotationId);
  }
}
