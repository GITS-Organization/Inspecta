import 'dart:typed_data';

import '../repositories/machines_repository.dart';

/// The certificate PDF the API generates for a machine.
class GetMachinePdf {
  final MachinesRepository _repository;

  const GetMachinePdf(this._repository);

  Future<Uint8List> call(String requestId, String requestItemId) {
    return _repository.getMachinePdf(requestId, requestItemId);
  }
}
