import 'package:cloud_firestore/cloud_firestore.dart';

/// `signatures/{uid}`: one document per technical manager (doc id == uid).
class SignatureRemoteDataSource {
  static const collection = 'signatures';

  final FirebaseFirestore? _firestoreOverride;

  SignatureRemoteDataSource({FirebaseFirestore? firestore}) : _firestoreOverride = firestore;

  FirebaseFirestore get _firestore => _firestoreOverride ?? FirebaseFirestore.instance;

  DocumentReference<Map<String, dynamic>> _doc(String managerId) => _firestore.collection(collection).doc(managerId);

  /// Empty until the manager has saved one.
  Stream<List<List<double>>> watchSignature(String managerId) {
    return _doc(managerId).snapshots().map((snapshot) {
      // Firestore can't nest arrays, so each stroke is stored as {'p': [...]}.
      return [
        for (final stroke in (snapshot.data()?['strokes'] as List<dynamic>? ?? const []))
          [
            for (final v in ((stroke as Map<String, dynamic>)['p'] as List<dynamic>? ?? const []))
              (v as num).toDouble(),
          ],
      ];
    });
  }

  /// Stamps `updatedAt` with the server time, as firestore.rules requires.
  Future<void> save(String managerId, List<List<double>> strokes) => _doc(managerId).set({
    'strokes': [
      for (final stroke in strokes) {'p': stroke},
    ],
    'updatedAt': FieldValue.serverTimestamp(),
  });
}
