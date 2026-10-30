import '../../../auth/domain/entities/user.dart';
import '../repositories/coordinator_repository.dart';

/// The inspectors a job can be assigned to, from the API.
class ListInspectors {
  final CoordinatorRepository _repository;

  const ListInspectors(this._repository);

  Future<List<AppUser>> call({String? managerId}) =>
      _repository.listInspectors(managerId: managerId);
}
