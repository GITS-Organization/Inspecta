import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/features/auth/data/models/user_model.dart';
import 'package:inspecta/features/auth/domain/entities/user_role.dart';

void main() {
  test('parses a users/{uid} document', () {
    final lastLogin = DateTime.utc(2026, 9, 1, 8);
    final model = UserModel.fromJson('u1', {
      'name': 'Karim Adel',
      'email': 'karim.adel@company.com',
      'role': 'technical_manager',
      'qualifications': ['Lifts', 42, 'Cranes'],
      'active': true,
      'lastLogin': Timestamp.fromDate(lastLogin),
    });

    final user = model.toEntity()!;
    expect(user.role, UserRole.technicalManager);
    expect(user.qualifications.map((q) => q.name), ['Lifts', 'Cranes']);
    expect(user.active, isTrue);
    expect(user.lastLogin!.isAtSameMomentAs(lastLogin), isTrue);
    expect(user.initials, 'KA');
  });

  test('treats a missing active flag as inactive', () {
    final model = UserModel.fromJson('u1', {'role': 'inspector'});
    expect(model.active, isFalse);
  });

  test('reads the retired supervisor role as a coordinator', () {
    final model = UserModel.fromJson('u1', {'role': 'supervisor', 'active': true});
    expect(model.toEntity()!.role, UserRole.coordinator);
    expect(UserRole.fromValue('coordinator'), UserRole.coordinator);
  });

  test('has no entity when the role is unknown', () {
    final model = UserModel.fromJson('u1', {'role': 'client', 'active': true});
    expect(model.toEntity(), isNull);
  });
}
