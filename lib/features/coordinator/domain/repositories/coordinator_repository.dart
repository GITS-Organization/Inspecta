import '../../../auth/domain/entities/user.dart';

/// Methods emit / throw [CoordinatorFailure] on expected errors.
abstract interface class CoordinatorRepository {
  /// The active inspectors of the user's team from the API, each marked on
  /// leave or not — backs the assign screen's list. [managerId] (Admin only)
  /// picks another team. Throws [CoordinatorFailure].
  Future<List<AppUser>> listInspectors({String? managerId});
}
