/// Each user has exactly one role in v1 (BR-01.1).
enum UserRole {
  coordinator('coordinator'),
  inspector('inspector'),
  technicalManager('technical_manager'),
  admin('admin');

  /// Value stored in the `role` field of a `users/{uid}` document.
  final String value;

  const UserRole(this.value);

  /// Accounts created while "supervisor" was still its own role are stored
  /// with that value; it was folded into [coordinator], so they read as one.
  static const _legacySupervisor = 'supervisor';

  static UserRole? fromValue(Object? value) {
    if (value == _legacySupervisor) return coordinator;
    for (final role in values) {
      if (role.value == value) return role;
    }
    return null;
  }
}
