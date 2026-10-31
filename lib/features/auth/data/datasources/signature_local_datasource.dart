import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// The signature a technical manager keeps in their profile, on this device.
///
/// The API has no endpoint to store one on its own — it only takes the
/// signature with an approval — so the profile holds it and each approval
/// sends it along. Kept per user, as the `data:image/png;base64,…` URL the
/// approval takes.
class SignatureLocalDataSource {
  static const _keyPrefix = 'profile_signature_';
  static const _strokesKeyPrefix = 'profile_signature_strokes_';

  final FlutterSecureStorage _storage;

  const SignatureLocalDataSource(this._storage);

  Future<String?> read(String userId) async {
    final value = await _storage.read(key: '$_keyPrefix$userId');
    return value == null || value.isEmpty ? null : value;
  }

  Future<void> save(String userId, String dataUrl) {
    return _storage.write(key: '$_keyPrefix$userId', value: dataUrl);
  }

  /// The strokes the signature was drawn with (flat, 0..1-normalised
  /// `[x, y, ...]` lists), which is what an approve-all takes — never the
  /// image. Null when none were kept, e.g. a signature saved before they were.
  Future<List<List<double>>?> readStrokes(String userId) async {
    final value = await _storage.read(key: '$_strokesKeyPrefix$userId');
    if (value == null || value.isEmpty) return null;
    try {
      final decoded = jsonDecode(value);
      if (decoded is! List) return null;
      return [
        for (final stroke in decoded)
          if (stroke is List) [for (final n in stroke) if (n is num) n.toDouble()],
      ];
    } catch (_) {
      return null;
    }
  }

  Future<void> saveStrokes(String userId, List<List<double>> strokes) {
    return _storage.write(key: '$_strokesKeyPrefix$userId', value: jsonEncode(strokes));
  }
}
