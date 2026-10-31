/// Where one machine's certificate stands, as the API reports it.
enum MachineStatus {
  notStarted('not_started'),
  draft('draft'),
  submitted('submitted'),
  returned('returned'),
  approved('approved'),
  delivered('delivered');

  final String value;

  const MachineStatus(this.value);

  static MachineStatus fromValue(Object? value) {
    for (final status in values) {
      if (status.value == value) return status;
    }
    return MachineStatus.notStarted;
  }
}
