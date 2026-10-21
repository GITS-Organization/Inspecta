import 'package:cloud_firestore/cloud_firestore.dart';

import '../models/timesheet_entry_model.dart';

class TimesheetRemoteDataSource {
  static const collection = 'timesheets';

  /// One doc per inspector (doc id == their uid) holding their hourly rate,
  /// which their prices are calculated from.
  static const settingsCollection = 'timesheetSettings';

  final FirebaseFirestore? _firestoreOverride;

  TimesheetRemoteDataSource({FirebaseFirestore? firestore}) : _firestoreOverride = firestore;

  FirebaseFirestore get _firestore => _firestoreOverride ?? FirebaseFirestore.instance;

  DocumentReference<Map<String, dynamic>> _doc(String requestId) =>
      _firestore.collection(collection).doc(requestId);

  List<TimesheetEntryModel> _models(QuerySnapshot<Map<String, dynamic>> snapshot) => [
    for (final doc in snapshot.docs) TimesheetEntryModel.fromJson(doc.id, doc.data()),
  ];

  /// The `where inspectorId ==` filter is required, not just convenient:
  /// firestore.rules only lets an inspector read their own entries, so an
  /// unfiltered query would be rejected outright.
  Stream<List<TimesheetEntryModel>> watchEntries(String inspectorId) {
    return _firestore
        .collection(collection)
        .where('inspectorId', isEqualTo: inspectorId)
        .snapshots()
        .map(_models);
  }

  /// The inspector's hourly rate in piastres; 0 until they set one.
  Stream<int> watchHourlyRate(String inspectorId) {
    return _firestore
        .collection(settingsCollection)
        .doc(inspectorId)
        .snapshots()
        .map((snapshot) => (snapshot.data()?['hourlyRatePiastres'] as num?)?.toInt() ?? 0);
  }

  /// Stamps `updatedAt` with the server time, as firestore.rules requires.
  Future<void> saveHourlyRate(String inspectorId, int hourlyRatePiastres) =>
      _firestore.collection(settingsCollection).doc(inspectorId).set({
        'hourlyRatePiastres': hourlyRatePiastres,
        'updatedAt': FieldValue.serverTimestamp(),
      });

  /// Stamps `updatedAt` with the server time — firestore.rules requires it
  /// to equal the request time, so a client clock can't be used.
  Future<void> save(TimesheetEntryModel model) =>
      _doc(model.requestId).set({...model.toJson(), 'updatedAt': FieldValue.serverTimestamp()}, SetOptions(merge: true));
}
