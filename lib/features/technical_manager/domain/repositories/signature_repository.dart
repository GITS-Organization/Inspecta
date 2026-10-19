/// A technical manager's own signature, drawn once and reused on every
/// certificate they approve. One flat, 0..1-normalised `[x, y, ...]` list per
/// pen stroke — the same shape the review's signature pad draws.
abstract interface class SignatureRepository {
  /// The manager's saved signature, or an empty list when they haven't set
  /// one. Emits again whenever it changes.
  Stream<List<List<double>>> watchSignature(String managerId);

  /// Saves (or replaces) the manager's signature.
  Future<void> saveSignature(String managerId, List<List<double>> strokes);
}
