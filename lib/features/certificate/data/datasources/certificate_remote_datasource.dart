import 'package:cloud_firestore/cloud_firestore.dart';

import '../models/certificate_model.dart';

class CertificateRemoteDataSource {
  static const collection = 'certificates';

  final FirebaseFirestore? _firestoreOverride;

  CertificateRemoteDataSource({FirebaseFirestore? firestore}) : _firestoreOverride = firestore;

  FirebaseFirestore get _firestore => _firestoreOverride ?? FirebaseFirestore.instance;

  DocumentReference<Map<String, dynamic>> _doc(String requestId) =>
      _firestore.collection(collection).doc(requestId);

  /// `null` until the inspector's first save creates the document — the
  /// doc id is always the job's request id (one certificate per job).
  Stream<CertificateModel?> watchCertificate(String requestId) {
    return _doc(requestId).snapshots().map((snapshot) {
      final data = snapshot.data();
      return data == null ? null : CertificateModel.fromJson(requestId, data);
    });
  }

  /// Certificates whose latest review (`reviewedAt`, server-stamped on both
  /// an approval and a send-back) is at or after [since].
  Stream<List<CertificateModel>> watchReviewedSince(DateTime since) {
    return _firestore
        .collection(collection)
        .where('reviewedAt', isGreaterThanOrEqualTo: Timestamp.fromDate(since))
        .snapshots()
        .map((snapshot) => [for (final doc in snapshot.docs) CertificateModel.fromJson(doc.id, doc.data())]);
  }

  Future<void> save(CertificateModel model) =>
      _doc(model.requestId).set(model.toJson(), SetOptions(merge: true));

  /// Same fields as [save], plus a server-stamped `submittedAt` — used
  /// only when the inspector actually submits (or resubmits), never on a
  /// regular autosave.
  Future<void> submit(CertificateModel model) => _doc(model.requestId).set({
    ...model.toJson(),
    'submittedAt': FieldValue.serverTimestamp(),
  }, SetOptions(merge: true));

  /// Feature 06: a technical manager's review — only the review fields,
  /// plus a server-stamped `reviewedAt`, matching the paired
  /// firestore.rules check.
  Future<void> saveReview(CertificateModel model) => _doc(model.requestId).set({
    ...model.toReviewJson(),
    'reviewedAt': FieldValue.serverTimestamp(),
  }, SetOptions(merge: true));
}
