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

  /// The reports the coordinator unticked; every other report goes.
  final Set<String> excludedReportIds;

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
    this.excludedReportIds = const {},
    this.isSending = false,
    this.actionSeq = 0,
    this.lastActionSuccess = false,
    this.lastResult,
    this.lastActionFailure,
  });

  /// The reports that go with the certificates: every one not unticked.
  List<DeliveryReport> get includedReports => [
    for (final report in preview?.reports ?? const <DeliveryReport>[])
      if (!excludedReportIds.contains(report.id)) report,
  ];

  /// What the email weighs: the certificate PDFs and the ticked reports, in raw
  /// bytes — the one sum the API's size limit is about.
  int get emailBytes =>
      (preview?.certificates ?? const <DeliveryCertificate>[]).fold(0, (sum, c) => sum + c.sizeBytes) +
      includedReports.fold(0, (sum, r) => sum + r.sizeBytes);

  /// With at least one report ticked, the email is over the size limit. (Only
  /// the API knows every byte; it answers `emailTooLarge` if this was wrong.)
  bool get isOverSizeLimit =>
      includedReports.isNotEmpty && emailBytes > (preview?.maxEmailBytes ?? DeliveryPreview.defaultMaxEmailBytes);

  /// Whether the preview lets the email go. The reports-too-large blocker is
  /// only a warning (unticking clears it, and the API decides in the end), so
  /// it doesn't stop it; any other blocker does.
  bool get _previewAllows {
    final preview = this.preview;
    if (preview == null) return false;
    return preview.canSend || (preview.blockers.isNotEmpty && preview.hardBlockers.isEmpty);
  }

  /// The API allows the email and there is an address to send it to.
  bool get canSend => _previewAllows && !isSending && Validators.isEmail(to);

  DeliveryEmailState copyWith({
    DeliveryEmailStatus? status,
    DeliveryPreview? preview,
    RequestsFailureCode? loadFailure,
    String? to,
    List<String>? cc,
    String? subject,
    String? body,
    Set<String>? excludedReportIds,
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
      excludedReportIds: excludedReportIds ?? this.excludedReportIds,
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
    excludedReportIds,
    isSending,
    actionSeq,
    lastActionSuccess,
    lastResult,
    lastActionFailure,
  ];
}
