part of 'my_signature_cubit.dart';

class MySignatureState extends Equatable {
  /// Whether the stored signature has been read yet.
  final bool loaded;

  /// The saved signature as a PNG data URL, or null when there isn't one.
  final String? signature;
  final bool isSaving;

  const MySignatureState({this.loaded = false, this.signature, this.isSaving = false});

  MySignatureState copyWith({bool? loaded, String? signature, bool? isSaving}) {
    return MySignatureState(
      loaded: loaded ?? this.loaded,
      signature: signature ?? this.signature,
      isSaving: isSaving ?? this.isSaving,
    );
  }

  @override
  List<Object?> get props => [loaded, signature, isSaving];
}
