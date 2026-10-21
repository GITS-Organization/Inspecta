import 'package:cloud_firestore/cloud_firestore.dart';

import '../models/client_model.dart';

class ClientsRemoteDataSource {
  static const collection = 'clients';

  final FirebaseFirestore? _firestoreOverride;

  ClientsRemoteDataSource({FirebaseFirestore? firestore}) : _firestoreOverride = firestore;

  FirebaseFirestore get _firestore => _firestoreOverride ?? FirebaseFirestore.instance;

  CollectionReference<Map<String, dynamic>> get _collection =>
      _firestore.collection(collection);

  /// Alphabetical by company name (the list screen's default sort).
  Stream<List<ClientModel>> watchClients() {
    return _collection
        .orderBy('companyName')
        .snapshots()
        .map((s) => s.docs.map((d) => ClientModel.fromJson(d.id, d.data())).toList());
  }

  Future<ClientModel?> getById(String id) async {
    final snapshot = await _collection.doc(id).get();
    final data = snapshot.data();
    return data == null ? null : ClientModel.fromJson(snapshot.id, data);
  }
}
