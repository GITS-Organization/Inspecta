import '../../../auth/domain/entities/user.dart';

/// Methods emit / throw [CoordinatorFailure] on expected errors.
abstract interface class CoordinatorRepository {
  /// Active inspectors, for choosing who to assign a job to (Feature 04
  /// §5's "Choose inspector" step). Coordinators can only read users with
  /// role `inspector` (see `firestore.rules`), so this is the one user
  /// list a coordinator's app can query.
  Stream<List<AppUser>> watchInspectors();

  /// The active inspectors of the user's team from the API, each marked on
  /// leave or not — backs the assign screen's list. [managerId] (Admin only)
  /// picks another team. Throws [CoordinatorFailure].
  Future<List<AppUser>> listInspectors({String? managerId});
}
