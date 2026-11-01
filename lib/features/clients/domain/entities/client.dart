import 'package:equatable/equatable.dart';

/// A client of the signed-in user's team.
class Client extends Equatable {
  final String id;
  final String name;
  final String? location;
  final String? certificatesEmail;

  const Client({required this.id, required this.name, this.location, this.certificatesEmail});

  @override
  List<Object?> get props => [id, name, location, certificatesEmail];
}

/// An address or a whole domain registered for a client: emails from it
/// become the client's requests. (The emails of the client's contacts and its
/// certificates email count as the client's too, without being listed.)
class ClientEmail extends Equatable {
  final String id;

  /// `buyer@alnoor.com`, or `alnoor.com` for a domain.
  final String pattern;
  final bool isDomain;

  /// How many "maybe" emails from this sender became requests of the client
  /// when it was added; only the answer to adding it carries one.
  final int promotedRequests;

  const ClientEmail({
    required this.id,
    required this.pattern,
    this.isDomain = false,
    this.promotedRequests = 0,
  });

  @override
  List<Object?> get props => [id, pattern, isDomain, promotedRequests];
}

/// A machine registered for a client; only these can be put on the client's
/// requests.
class ClientMachine extends Equatable {
  final String id;
  final String name;
  final String? category;
  final String? manufacturer;
  final String? model;
  final String? serialNumber;
  final String? capacity;

  const ClientMachine({
    required this.id,
    required this.name,
    this.category,
    this.manufacturer,
    this.model,
    this.serialNumber,
    this.capacity,
  });

  /// What identifies the machine on site: capacity, make, model and serial.
  String get details => [capacity, manufacturer, model, serialNumber]
      .whereType<String>()
      .map((part) => part.trim())
      .where((part) => part.isNotEmpty)
      .join(' · ');

  @override
  List<Object?> get props => [id, name, category, manufacturer, model, serialNumber, capacity];
}
