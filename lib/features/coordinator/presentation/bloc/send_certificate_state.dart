part of 'send_certificate_cubit.dart';

enum SendCertificateStatus { loading, ready, notFound, error }

class SendCertificateState extends Equatable {
  final SendCertificateStatus status;
  final Certificate? certificate;
  final String email;

  /// The address [SendCertificateCubit] looked up from the client, so the
  /// screen can drop it into the email field once.
  final String? suggestedEmail;

  final bool isSending;
  final int actionSeq;
  final bool lastActionSuccess;
  final CertificateFailureCode? lastActionFailure;

  const SendCertificateState({
    this.status = SendCertificateStatus.loading,
    this.certificate,
    this.email = '',
    this.suggestedEmail,
    this.isSending = false,
    this.actionSeq = 0,
    this.lastActionSuccess = false,
    this.lastActionFailure,
  });

  /// Addressed to something that looks like an email, and not already going.
  bool get canSend => certificate != null && !isSending && Validators.isEmail(email);

  SendCertificateState copyWith({
    SendCertificateStatus? status,
    Certificate? certificate,
    String? email,
    String? suggestedEmail,
    bool? isSending,
    int? actionSeq,
    bool? lastActionSuccess,
    CertificateFailureCode? lastActionFailure,
  }) {
    return SendCertificateState(
      status: status ?? this.status,
      certificate: certificate ?? this.certificate,
      email: email ?? this.email,
      suggestedEmail: suggestedEmail ?? this.suggestedEmail,
      isSending: isSending ?? this.isSending,
      actionSeq: actionSeq ?? this.actionSeq,
      lastActionSuccess: lastActionSuccess ?? this.lastActionSuccess,
      lastActionFailure: lastActionFailure ?? this.lastActionFailure,
    );
  }

  @override
  List<Object?> get props => [
    status,
    certificate,
    email,
    suggestedEmail,
    isSending,
    actionSeq,
    lastActionSuccess,
    lastActionFailure,
  ];
}
