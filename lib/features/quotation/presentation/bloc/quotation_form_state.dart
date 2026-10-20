part of 'quotation_form_cubit.dart';

enum QuotationAction { saveDraft, send }

class QuotationFormState extends Equatable {
  final ReplyType? replyType;
  /// Typed price (EGP) and pricing unit per request item, keyed by item id.
  final Map<String, String> linePrices;
  final Map<String, PricingUnit> lineUnits;

  /// The proposal's notes, one point each; [defaultProposalNotes] until edited.
  final List<String> notes;

  /// The request's item ids, so validation knows which lines need a price.
  final List<String> itemIds;
  final int validityDays;
  final String message;

  /// Field errors show only after a send attempt, then update live.
  final bool submitted;

  final bool isSubmitting;

  /// Bumped whenever [QuotationAction.saveDraft] or [.send] finishes, so a
  /// listener can react exactly once per action (BlocListener's
  /// `listenWhen` compares this, not the whole state).
  final int actionSeq;
  final QuotationAction? lastAction;
  final bool lastActionSuccess;
  final QuotationFailureCode? lastActionFailure;
  final int? sentVersion;

  const QuotationFormState({
    this.replyType,
    this.linePrices = const {},
    this.lineUnits = const {},
    this.notes = defaultProposalNotes,
    this.itemIds = const [],
    this.validityDays = 14,
    this.message = '',
    this.submitted = false,
    this.isSubmitting = false,
    this.actionSeq = 0,
    this.lastAction,
    this.lastActionSuccess = false,
    this.lastActionFailure,
    this.sentVersion,
  });

  int? unitPriceOf(String itemId) => Currency.parsePiastres(linePrices[itemId] ?? '');

  PricingUnit unitOf(String itemId) => lineUnits[itemId] ?? PricingUnit.perVisit;

  bool linePriceInvalid(String itemId) {
    final price = unitPriceOf(itemId);
    return price == null || price <= 0;
  }

  /// An offer needs every line priced (the budget table prints them all).
  bool get priceInvalid =>
      replyType == ReplyType.offer && itemIds.any(linePriceInvalid);

  bool get reasonInvalid => replyType == ReplyType.reject && message.trim().isEmpty;

  bool get showPriceError => submitted && priceInvalid;

  bool get showReasonError => submitted && reasonInvalid;

  /// Whether Send may be pressed at all (acceptance criterion 2 — reject
  /// needs a reason, an offer needs a price).
  bool get isValid => replyType != null && !priceInvalid && !reasonInvalid;

  QuotationFormState copyWith({
    ReplyType? replyType,
    Map<String, String>? linePrices,
    Map<String, PricingUnit>? lineUnits,
    List<String>? notes,
    int? validityDays,
    String? message,
    bool? submitted,
    bool? isSubmitting,
    int? actionSeq,
    QuotationAction? lastAction,
    bool? lastActionSuccess,
    QuotationFailureCode? lastActionFailure,
    bool clearFailure = false,
    int? sentVersion,
  }) {
    return QuotationFormState(
      replyType: replyType ?? this.replyType,
      linePrices: linePrices ?? this.linePrices,
      lineUnits: lineUnits ?? this.lineUnits,
      notes: notes ?? this.notes,
      itemIds: itemIds,
      validityDays: validityDays ?? this.validityDays,
      message: message ?? this.message,
      submitted: submitted ?? this.submitted,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      actionSeq: actionSeq ?? this.actionSeq,
      lastAction: lastAction ?? this.lastAction,
      lastActionSuccess: lastActionSuccess ?? this.lastActionSuccess,
      lastActionFailure: clearFailure ? null : (lastActionFailure ?? this.lastActionFailure),
      sentVersion: sentVersion ?? this.sentVersion,
    );
  }

  @override
  List<Object?> get props => [
    replyType,
    linePrices,
    lineUnits,
    notes,
    itemIds,
    validityDays,
    message,
    submitted,
    isSubmitting,
    actionSeq,
    lastAction,
    lastActionSuccess,
    lastActionFailure,
    sentVersion,
  ];
}
