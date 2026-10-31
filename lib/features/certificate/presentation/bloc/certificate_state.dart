part of 'certificate_cubit.dart';

enum CertificateStatus { loading, ready, error }

enum CertificateSaveStatus { idle, saving, saved, error }

class CertificateState extends Equatable {
  final CertificateStatus status;
  final Certificate? certificate;
  final CertificateFailureCode? failure;
  final CertificateSaveStatus saveStatus;

  final bool isSubmitting;
  final int actionSeq;
  final bool lastActionSuccess;
  final CertificateFailureCode? lastActionFailure;

  // Only when the form is open on one machine through the machines API.

  /// The form is working on a machine of the API rather than on the job's
  /// Firestore certificate.
  final bool machineMode;

  /// The machine's own technical fields (manufacturer, serial number…), as
  /// the API lists them for this kind of machine.
  final List<MachineFormField> machineFields;

  /// The machine's checklist: sections of rows answered yes / no / n/a.
  final List<MachineFormSection> checklist;

  /// The item being inspected, as the API names it.
  final String inspectedItem;

  /// Whether the API would take a submit now, and what stops one when not.
  final bool apiReady;
  final List<String> missing;

  const CertificateState({
    this.status = CertificateStatus.loading,
    this.certificate,
    this.failure,
    this.saveStatus = CertificateSaveStatus.idle,
    this.isSubmitting = false,
    this.actionSeq = 0,
    this.lastActionSuccess = false,
    this.lastActionFailure,
    this.machineMode = false,
    this.machineFields = const [],
    this.checklist = const [],
    this.inspectedItem = '',
    this.apiReady = false,
    this.missing = const [],
  });

  CertificateStep get currentStep => certificate.currentStep;

  int get currentStepNumber => certificate.currentStepNumber;

  int get totalSteps => certificate.totalSteps;

  /// Whether the submit button is on: the API's say for a machine, the form's
  /// own completeness rules otherwise.
  bool get canSubmit => !isSubmitting && (machineMode ? apiReady : (certificate?.isReadyToSubmit ?? false));

  CertificateState copyWith({
    CertificateStatus? status,
    Certificate? certificate,
    CertificateFailureCode? failure,
    bool clearFailure = false,
    CertificateSaveStatus? saveStatus,
    bool? isSubmitting,
    int? actionSeq,
    bool? lastActionSuccess,
    CertificateFailureCode? lastActionFailure,
    bool? machineMode,
    List<MachineFormField>? machineFields,
    List<MachineFormSection>? checklist,
    String? inspectedItem,
    bool? apiReady,
    List<String>? missing,
  }) {
    return CertificateState(
      status: status ?? this.status,
      certificate: certificate ?? this.certificate,
      failure: clearFailure ? null : (failure ?? this.failure),
      saveStatus: saveStatus ?? this.saveStatus,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      actionSeq: actionSeq ?? this.actionSeq,
      lastActionSuccess: lastActionSuccess ?? this.lastActionSuccess,
      lastActionFailure: lastActionFailure ?? this.lastActionFailure,
      machineMode: machineMode ?? this.machineMode,
      machineFields: machineFields ?? this.machineFields,
      checklist: checklist ?? this.checklist,
      inspectedItem: inspectedItem ?? this.inspectedItem,
      apiReady: apiReady ?? this.apiReady,
      missing: missing ?? this.missing,
    );
  }

  @override
  List<Object?> get props => [
    status,
    certificate,
    failure,
    saveStatus,
    isSubmitting,
    actionSeq,
    lastActionSuccess,
    lastActionFailure,
    machineMode,
    machineFields,
    checklist,
    inspectedItem,
    apiReady,
    missing,
  ];
}
