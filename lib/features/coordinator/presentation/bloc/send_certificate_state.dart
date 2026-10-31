part of 'send_certificate_cubit.dart';

enum SendCertificateStatus { loading, ready, error }

class SendCertificateState extends Equatable {
  final SendCertificateStatus status;

  /// The job's machines API answer: its certificates and whether it can be
  /// delivered.
  final JobMachines? job;

  /// The email that would go to the client; null when it couldn't be read.
  final DeliveryPreview? preview;
  final RequestsFailureCode? loadFailure;
  final String email;
  final String message;

  /// The address the preview named, so the screen can drop it into the email
  /// field once.
  final String? suggestedEmail;

  final bool isSending;
  final int actionSeq;
  final bool lastActionSuccess;
  final DeliveryResult? lastResult;
  final CertificateFailureCode? lastActionFailure;

  const SendCertificateState({
    this.status = SendCertificateStatus.loading,
    this.job,
    this.preview,
    this.loadFailure,
    this.email = '',
    this.message = '',
    this.suggestedEmail,
    this.isSending = false,
    this.actionSeq = 0,
    this.lastActionSuccess = false,
    this.lastResult,
    this.lastActionFailure,
  });

  /// The machines with a certificate to hand over.
  List<MachineSummary> get certificates => (job?.machines ?? const <MachineSummary>[])
      .where((m) => m.status != MachineStatus.notStarted && m.status != MachineStatus.draft)
      .toList();

  bool get canDeliver => job?.canDeliver ?? false;

  /// The API allows the delivery and the address, if one is typed, looks like
  /// an email — left empty, the API uses the client's own.
  bool get canSend => canDeliver && !isSending && (email.trim().isEmpty || Validators.isEmail(email.trim()));

  SendCertificateState copyWith({
    SendCertificateStatus? status,
    JobMachines? job,
    DeliveryPreview? preview,
    RequestsFailureCode? loadFailure,
    String? email,
    String? message,
    String? suggestedEmail,
    bool? isSending,
    int? actionSeq,
    bool? lastActionSuccess,
    DeliveryResult? lastResult,
    CertificateFailureCode? lastActionFailure,
  }) {
    return SendCertificateState(
      status: status ?? this.status,
      job: job ?? this.job,
      preview: preview ?? this.preview,
      loadFailure: loadFailure ?? this.loadFailure,
      email: email ?? this.email,
      message: message ?? this.message,
      suggestedEmail: suggestedEmail ?? this.suggestedEmail,
      isSending: isSending ?? this.isSending,
      actionSeq: actionSeq ?? this.actionSeq,
      lastActionSuccess: lastActionSuccess ?? this.lastActionSuccess,
      lastResult: lastResult ?? this.lastResult,
      lastActionFailure: lastActionFailure ?? this.lastActionFailure,
    );
  }

  @override
  List<Object?> get props => [
    status,
    job,
    preview,
    loadFailure,
    email,
    message,
    suggestedEmail,
    isSending,
    actionSeq,
    lastActionSuccess,
    lastResult,
    lastActionFailure,
  ];
}
