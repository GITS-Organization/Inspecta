import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../domain/usecases/save_signature.dart';
import '../../domain/usecases/watch_signature.dart';

part 'my_signature_state.dart';

/// Backs the technical manager's "My signature" card on the Profile screen:
/// the signature they've saved, and setting a new one.
class MySignatureCubit extends Cubit<MySignatureState> {
  final WatchSignature _watchSignature;
  final SaveSignature _saveSignature;

  String? _managerId;
  StreamSubscription<List<List<double>>>? _subscription;

  MySignatureCubit(this._watchSignature, this._saveSignature) : super(const MySignatureState());

  void start(String managerId) {
    if (_subscription != null) return;
    _managerId = managerId;
    _subscription = _watchSignature(managerId).listen(
      (strokes) => emit(state.copyWith(strokes: strokes)),
      // Can't be read: show it as not set rather than as loading forever.
      onError: (_) => emit(state.copyWith(strokes: const [])),
    );
  }

  /// Saves [strokes] as the manager's signature. Returns whether it worked.
  Future<bool> save(List<List<double>> strokes) async {
    final managerId = _managerId;
    if (managerId == null || state.isSaving) return false;
    emit(state.copyWith(isSaving: true));
    try {
      await _saveSignature(managerId, strokes);
      if (!isClosed) emit(state.copyWith(isSaving: false));
      return true;
    } catch (_) {
      if (!isClosed) emit(state.copyWith(isSaving: false));
      return false;
    }
  }

  @override
  Future<void> close() async {
    await _subscription?.cancel();
    return super.close();
  }
}
