import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/widgets/signature_image.dart';
import '../../data/datasources/signature_local_datasource.dart';

part 'my_signature_state.dart';

/// Backs the technical manager's "My signature" card on the Profile screen:
/// the signature they've saved, and drawing a new one.
class MySignatureCubit extends Cubit<MySignatureState> {
  final SignatureLocalDataSource _store;
  final String _userId;

  MySignatureCubit(this._store, this._userId) : super(const MySignatureState());

  Future<void> start() async {
    try {
      final signature = await _store.read(_userId);
      if (!isClosed) emit(state.copyWith(loaded: true, signature: signature));
    } catch (_) {
      // Can't be read: show it as not set rather than as loading forever.
      if (!isClosed) emit(state.copyWith(loaded: true));
    }
  }

  /// Saves [strokes] as the manager's signature. Returns whether it worked.
  Future<bool> save(List<List<double>> strokes) async {
    if (state.isSaving) return false;
    emit(state.copyWith(isSaving: true));
    try {
      final signature = await signatureDataUrl(strokes);
      await _store.save(_userId, signature);
      await _store.saveStrokes(_userId, strokes);
      if (!isClosed) emit(state.copyWith(isSaving: false, signature: signature));
      return true;
    } catch (_) {
      if (!isClosed) emit(state.copyWith(isSaving: false));
      return false;
    }
  }
}
