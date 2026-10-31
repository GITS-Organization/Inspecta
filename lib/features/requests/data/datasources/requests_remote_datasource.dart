import 'package:cloud_firestore/cloud_firestore.dart';

import '../../../../core/enums/job_status.dart';
import '../models/inspection_request_model.dart';

class RequestsRemoteDataSource {
  static const collection = 'requests';

  /// Subcollection of `JobEvent`s per request (00-overview.md §7/§8: every
  /// status change is logged and can't be edited).
  static const eventsSubcollection = 'events';

  final FirebaseFirestore? _firestoreOverride;

  /// The signed-in user's id, as the Inspecta API knows them.
  final String? Function() _currentUserId;

  RequestsRemoteDataSource({
    FirebaseFirestore? firestore,
    String? Function()? currentUserId,
  })
      : _firestoreOverride = firestore,
        _currentUserId = currentUserId ?? (() => null);

  FirebaseFirestore get _firestore =>
      _firestoreOverride ?? FirebaseFirestore.instance;

  DocumentReference<Map<String, dynamic>> _doc(String id) =>
      _firestore.collection(collection).doc(id);

  /// Newest first (BR-02.7's inbox sort).
  Stream<List<InspectionRequestModel>> watchRequests() {
    return _firestore
        .collection(collection)
        .orderBy('receivedAt', descending: true)
        .snapshots()
        .map(
          (snapshot) =>
          snapshot.docs
              .map((doc) => InspectionRequestModel.fromJson(doc.id, doc.data()))
              .toList(),
    );
  }

  Future<InspectionRequestModel?> getById(String id) async {
    final snapshot = await _doc(id).get();
    final data = snapshot.data();
    return data == null ? null : InspectionRequestModel.fromJson(
        snapshot.id, data);
  }

  Future<void> updateStatus(String requestId,
      JobStatus status, {
        String? note,
        Map<String, dynamic>? extra,
      }) async {
    final batch = _firestore.batch();
    batch.update(_doc(requestId), {'status': status.value, ...?extra});
    batch.set(_doc(requestId).collection(eventsSubcollection).doc(), {
      'toStatus': status.value,
      'actorId': _currentUserId(),
      'note': note,
      'timestamp': FieldValue.serverTimestamp(),
    });
    await batch.commit();
  }
}
