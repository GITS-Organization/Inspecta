part of 'send_certificate_cubit.dart';

enum SendCertificateStatus { loading, ready, error }

class SendCertificateState extends Equatable {
  final SendCertificateStatus status;

  /// The job's machines API answer: its certificates, whether it can be
  /// delivered and, afterwards, what was sent.
  final JobMachines? job;
  final RequestsFailureCode? loadFailure;

  const SendCertificateState({this.status = SendCertificateStatus.loading, this.job, this.loadFailure});

  /// The machines with a certificate to hand over.
  List<MachineSummary> get certificates => (job?.machines ?? const <MachineSummary>[])
      .where((m) => m.status != MachineStatus.notStarted && m.status != MachineStatus.draft)
      .toList();

  bool get canDeliver => job?.canDeliver ?? false;

  /// What went out to the client, once the job has been delivered.
  DeliveryResult? get delivery => job?.delivery;

  SendCertificateState copyWith({
    SendCertificateStatus? status,
    JobMachines? job,
    RequestsFailureCode? loadFailure,
  }) {
    return SendCertificateState(
      status: status ?? this.status,
      job: job ?? this.job,
      loadFailure: loadFailure ?? this.loadFailure,
    );
  }

  @override
  List<Object?> get props => [status, job, loadFailure];
}
