part of 'request_suggestions_cubit.dart';

enum SuggestionsStatus { loading, ready }

class RequestSuggestionsState extends Equatable {
  /// The request as it now is (it changes when a suggestion is added or the
  /// request is filled from its email).
  final InspectionRequest request;
  final SuggestionsStatus status;
  final List<MachineSuggestion> suggestions;

  /// The machine being added, while it is.
  final String? addingId;
  final bool isAutofilling;

  const RequestSuggestionsState({
    required this.request,
    this.status = SuggestionsStatus.loading,
    this.suggestions = const [],
    this.addingId,
    this.isAutofilling = false,
  });

  bool get busy => addingId != null || isAutofilling;

  RequestSuggestionsState copyWith({
    InspectionRequest? request,
    SuggestionsStatus? status,
    List<MachineSuggestion>? suggestions,
    String? addingId,
    bool clearAddingId = false,
    bool? isAutofilling,
  }) {
    return RequestSuggestionsState(
      request: request ?? this.request,
      status: status ?? this.status,
      suggestions: suggestions ?? this.suggestions,
      addingId: clearAddingId ? null : (addingId ?? this.addingId),
      isAutofilling: isAutofilling ?? this.isAutofilling,
    );
  }

  @override
  List<Object?> get props => [request, status, suggestions, addingId, isAutofilling];
}
