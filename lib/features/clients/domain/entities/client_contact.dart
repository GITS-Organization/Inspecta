import 'package:equatable/equatable.dart';

/// A person at a client company — who a coordinator calls when starting a
/// new job, and who certificates might be addressed to.
class ClientContact extends Equatable {
  final String name;
  final String? role;
  final String? email;
  final String? phone;

  /// The contact a call/email button and quote correspondence default
  /// to. Exactly one contact should carry this, but a fresh import or a
  /// hand-edited record could momentarily have none — callers fall back
  /// to the first contact rather than assuming one exists.
  final bool isMain;

  const ClientContact({
    required this.name,
    this.role,
    this.email,
    this.phone,
    this.isMain = false,
  });

  @override
  List<Object?> get props => [name, role, email, phone, isMain];
}
