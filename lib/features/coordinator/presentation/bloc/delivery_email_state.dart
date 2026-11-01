part of 'delivery_email_cubit.dart';

enum DeliveryEmailStatus { loading, ready, error }

class DeliveryEmailState extends Equatable {
  final DeliveryEmailStatus status;

  /// The email as the API proposes it; null until loaded.
  final DeliveryPreview? preview;
  final RequestsFailureCode? loadFailure;

  /// What the coordinator has in the form (the preview's values until
  /// edited).
  final String to;
  final List<String> cc;
  final String subject;
  final String body;

  final bool isSending;
  final int actionSeq;
  final bool lastActionSuccess;
  final DeliveryResult? lastResult;
  final DeliveryFailureCode? lastActionFailure;

  const DeliveryEmailState({
    this.status = DeliveryEmailStatus.loading,
    this.preview,
    this.loadFailure,
    this.to = '',
    this.cc = const [],
    this.subject = '',
    this.body = '',
    this.isSending = false,
    this.actionSeq = 0,
    this.lastActionSuccess = false,
    this.lastResult,
    this.lastActionFailure,
  });

  /// The API allows the email (no blockers) and there is an address to send
  /// it to.
  bool get canSend => (preview?.canSend ?? false) && !isSending && Validators.isEmail(to);

  DeliveryEmailState copyWith({
    DeliveryEmailStatus? status,
    DeliveryPreview? preview,
    RequestsFailureCode? loadFailure,
    String? to,
    List<String>? cc,
    String? subject,
    String? body,
    bool? isSending,
    int? actionSeq,
    bool? lastActionSuccess,
    DeliveryResult? lastResult,
    DeliveryFailureCode? lastActionFailure,
  }) {
    return DeliveryEmailState(
      status: status ?? this.status,
      preview: preview ?? this.preview,
      loadFailure: loadFailure ?? this.loadFailure,
      to: to ?? this.to,
      cc: cc ?? this.cc,
      subject: subject ?? this.subject,
      body: body ?? this.body,
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
    preview,
    loadFailure,
    to,
    cc,
    subject,
    body,
    isSending,
    actionSeq,
    lastActionSuccess,
    lastResult,
    lastActionFailure,
  ];
}
