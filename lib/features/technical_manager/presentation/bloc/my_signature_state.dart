part of 'my_signature_cubit.dart';

class MySignatureState extends Equatable {
  /// The saved signature — empty when there isn't one, null until it has
  /// loaded.
  final List<List<double>>? strokes;
  final bool isSaving;

  const MySignatureState({this.strokes, this.isSaving = false});

  bool get isLoaded => strokes != null;

  bool get hasSignature => strokes?.any((stroke) => stroke.length >= 4) ?? false;

  MySignatureState copyWith({List<List<double>>? strokes, bool? isSaving}) =>
      MySignatureState(strokes: strokes ?? this.strokes, isSaving: isSaving ?? this.isSaving);

  @override
  List<Object?> get props => [strokes, isSaving];
}
