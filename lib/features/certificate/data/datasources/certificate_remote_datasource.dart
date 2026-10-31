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

  /// The coordinator sending an approved certificate on: just who it went to.
  Future<void> saveSentTo(String requestId, String email) =>
      _doc(requestId).set({'sentTo': email}, SetOptions(merge: true));
}
