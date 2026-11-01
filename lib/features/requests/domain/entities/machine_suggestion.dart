import 'package:equatable/equatable.dart';

/// How sure the API is that an email asks about a machine.
enum SuggestionLevel {
  /// Clear: added on its own when the request has no items.
  high,

  /// A suggestion only.
  medium,

  /// Several similar machines fit (a 2 t and a 5 t sling, say): the
  /// coordinator picks.
  ambiguous;

  static SuggestionLevel fromValue(Object? value) => switch (value) {
    'high' => high,
    'ambiguous' => ambiguous,
    _ => medium,
  };
}

/// A machine of the request's client that its email seems to ask about
/// (`GET /requests/{id}/suggestions`). Accepting it adds an item linked to
/// [equipmentId].
class MachineSuggestion extends Equatable {
  final String equipmentId;
  final String name;

  /// The quantity read from the email (1 when it names none).
  final int quantity;

  /// 0 to 1.
  final double confidence;
  final SuggestionLevel level;

  /// How it was found: `serial`, `name` or `model`.
  final String? reason;

  /// The line of the email that mentions it.
  final String? evidence;

  const MachineSuggestion({
    required this.equipmentId,
    required this.name,
    this.quantity = 1,
    this.confidence = 0,
    this.level = SuggestionLevel.medium,
    this.reason,
    this.evidence,
  });

  @override
  List<Object?> get props => [equipmentId, name, quantity, confidence, level, reason, evidence];
}
