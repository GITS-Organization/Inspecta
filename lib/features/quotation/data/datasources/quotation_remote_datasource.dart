import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';

import '../../domain/entities/client_response_outcome.dart';
import '../../domain/entities/quote_line_item.dart';
import '../../domain/entities/reply_type.dart';
import '../models/quotation_model.dart';
import '../models/quote_line_item_model.dart';

class QuotationRemoteDataSource {
  static const collection = 'quotations';

  final FirebaseFirestore? _firestoreOverride;
  final FirebaseAuth? _authOverride;

  QuotationRemoteDataSource({FirebaseFirestore? firestore, FirebaseAuth? auth})
    : _firestoreOverride = firestore,
      _authOverride = auth;

  FirebaseFirestore get _firestore =>
      _firestoreOverride ?? FirebaseFirestore.instance;

  FirebaseAuth get _auth => _authOverride ?? FirebaseAuth.instance;

  CollectionReference<Map<String, dynamic>> get _collection =>
      _firestore.collection(collection);

  Stream<List<QuotationModel>> watchAll() {
    return _collection
        .orderBy('createdAt', descending: true)
        .snapshots()
        .map((s) => s.docs.map((d) => QuotationModel.fromJson(d.id, d.data())).toList());
  }

  Stream<List<QuotationModel>> watchForRequest(String requestId) {
    return _collection
        .where('requestId', isEqualTo: requestId)
        .orderBy('createdAt', descending: true)
        .snapshots()
        .map((s) => s.docs.map((d) => QuotationModel.fromJson(d.id, d.data())).toList());
  }

  Future<DocumentReference<Map<String, dynamic>>?> _findDraft(String requestId) async {
    final snapshot = await _collection
        .where('requestId', isEqualTo: requestId)
        .where('status', isEqualTo: 'draft')
        .limit(1)
        .get();
    return snapshot.docs.isEmpty ? null : snapshot.docs.first.reference;
  }

  Future<int> _nextVersion(String requestId) async {
    final snapshot = await _collection
        .where('requestId', isEqualTo: requestId)
        .where('status', whereNotIn: ['draft'])
        .count()
        .get();
    return (snapshot.count ?? 0) + 1;
  }

  Map<String, dynamic> _priceFields(
    ReplyType type,
    List<QuoteLineItemModel> items,
    int? validityDays,
  ) {
    if (type != ReplyType.offer) {
      return {'subtotalPiastres': null, 'totalPiastres': null, 'validUntil': null};
    }
    final total = items.fold<int>(
      0,
      (runningTotal, item) => runningTotal + item.unitPricePiastres * item.quantity,
    );
    final validUntil = validityDays == null
        ? null
        : Timestamp.fromDate(DateTime.now().add(Duration(days: validityDays)));
    // BR-03.2: no VAT line yet — see the quotation feature's known gaps.
    return {'subtotalPiastres': total, 'totalPiastres': total, 'validUntil': validUntil};
  }

  Future<QuotationModel> saveDraft({
    required String requestId,
    required String requestNumber,
    required String clientName,
    required String equipmentSummary,
    required ReplyType type,
    required List<QuoteLineItem> items,
    int? validityDays,
    String? message,
    List<String>? notes,
  }) async {
    final itemModels = items.map(QuoteLineItemModel.fromEntity).toList();
    final data = {
      'requestId': requestId,
      'requestNumber': requestNumber,
      'clientName': clientName,
      'equipmentSummary': equipmentSummary,
      'type': type.value,
      'items': itemModels.map((i) => i.toJson()).toList(),
      ..._priceFields(type, itemModels, validityDays),
      'message': message,
      'notes': notes,
      'status': 'draft',
      'createdBy': _auth.currentUser?.uid,
      'updatedAt': FieldValue.serverTimestamp(),
    };

    final existing = await _findDraft(requestId);
    if (existing != null) {
      await existing.update(data);
      final snapshot = await existing.get();
      return QuotationModel.fromJson(snapshot.id, snapshot.data()!);
    }

    final ref = await _collection.add({...data, 'createdAt': FieldValue.serverTimestamp()});
    final snapshot = await ref.get();
    return QuotationModel.fromJson(snapshot.id, snapshot.data()!);
  }

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
  }) async {
    final itemModels = items.map(QuoteLineItemModel.fromEntity).toList();
    final version = await _nextVersion(requestId);
    final data = {
      'requestId': requestId,
      'requestNumber': requestNumber,
      'clientName': clientName,
      'equipmentSummary': equipmentSummary,
      'version': version,
      'type': type.value,
      'items': itemModels.map((i) => i.toJson()).toList(),
      ..._priceFields(type, itemModels, validityDays),
      'message': message,
      'notes': notes,
      'status': 'sent',
      'sentAt': FieldValue.serverTimestamp(),
      'createdBy': _auth.currentUser?.uid,
      'updatedAt': FieldValue.serverTimestamp(),
    };

    final existing = await _findDraft(requestId);
    if (existing != null) {
      await existing.update(data);
    } else {
      await _collection.add({...data, 'createdAt': FieldValue.serverTimestamp()});
    }
    return version;
  }

  /// BR-03.6/03.7: records the client's answer, and — when accepted —
  /// supersedes every other non-superseded quotation on the request in
  /// the same batch (the agreed price is locked to just this version).
  Future<void> recordClientResponse({
    required String quotationId,
    required String requestId,
    required ClientResponseOutcome outcome,
    int? clientPricePiastres,
    String? note,
  }) async {
    // ClientResponseOutcome.counter's value ('counter') isn't a valid
    // QuotationStatus (which is 'countered') — map explicitly rather than
    // reusing outcome.value, which would write an unparseable status and
    // make the quotation vanish from every list.
    final status = switch (outcome) {
      ClientResponseOutcome.counter => 'countered',
      ClientResponseOutcome.accepted => 'accepted',
      ClientResponseOutcome.declined => 'declined',
    };
    final batch = _firestore.batch();
    batch.update(_collection.doc(quotationId), {
      'status': status,
      'clientResponseOutcome': outcome.value,
      'clientCounterPricePiastres': clientPricePiastres,
      'clientResponseNote': note,
      'clientRespondedAt': FieldValue.serverTimestamp(),
      'updatedAt': FieldValue.serverTimestamp(),
    });

    if (outcome == ClientResponseOutcome.accepted) {
      final siblings = await _collection
          .where('requestId', isEqualTo: requestId)
          .where('status', whereIn: ['sent', 'countered'])
          .get();
      for (final doc in siblings.docs) {
        if (doc.id == quotationId) continue;
        batch.update(doc.reference, {
          'status': 'superseded',
          'updatedAt': FieldValue.serverTimestamp(),
        });
      }
    }

    await batch.commit();
  }

  Future<void> sendReminder(String quotationId) {
    return _collection.doc(quotationId).update({
      'lastReminderAt': FieldValue.serverTimestamp(),
      'updatedAt': FieldValue.serverTimestamp(),
    });
  }
}
