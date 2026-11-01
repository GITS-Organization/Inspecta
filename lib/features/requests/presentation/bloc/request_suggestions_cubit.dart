import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../domain/entities/inspection_request.dart';
import '../../domain/entities/machine_suggestion.dart';
import '../../domain/entities/request_item.dart';
import '../../domain/entities/requests_failure.dart';
import '../../domain/usecases/autofill_request.dart';
import '../../domain/usecases/complete_intake.dart';
import '../../domain/usecases/get_machine_suggestions.dart';
import '../../domain/usecases/get_request_detail.dart';

part 'request_suggestions_state.dart';

/// Backs the request screen's "From the email" panel: the client's machines
/// the email seems to ask about, which the coordinator adds to the request one
/// by one, and "Fill from email" for a request with no items yet.
///
/// Adding or filling changes the request, so [RequestSuggestionsState.request]
/// always holds it as it now is; the screen shows that one.
class RequestSuggestionsCubit extends Cubit<RequestSuggestionsState> {
  final GetMachineSuggestions _getSuggestions;
  final AutofillRequest _autofill;
  final CompleteIntake _completeIntake;
  final GetRequestDetail _getRequest;

  RequestSuggestionsCubit({
    required InspectionRequest request,
    required this._getSuggestions,
    required this._autofill,
    required this._completeIntake,
    required this._getRequest,
  }) : super(RequestSuggestionsState(request: request));

  /// Loads the suggestions — also after the request changed. A failure leaves
  /// the panel empty: the suggestions are a courtesy.
  Future<void> start() async {
    try {
      final suggestions = await _getSuggestions(state.request.id);
      if (isClosed) return;
      emit(
        state.copyWith(
          status: SuggestionsStatus.ready,
          suggestions: suggestions,
        ),
      );
    } catch (_) {
      if (isClosed) return;
      emit(
        state.copyWith(status: SuggestionsStatus.ready, suggestions: const []),
      );
    }
  }

  /// Adds [suggestion] to the request as an item linked to its machine.
  /// Returns the failure's message code when it didn't work, null when it did.
  Future<RequestsFailure?> add(MachineSuggestion suggestion) async {
    if (state.busy) return null;
    final request = state.request;
    emit(state.copyWith(addingId: suggestion.equipmentId));
    try {
      await _completeIntake(
        request: request,
        clientName: request.clientName,
        location: request.location,
        accessNotes: request.accessNotes,
        items: [
          ...request.items,
          RequestItem(
            id: '',
            equipmentId: suggestion.equipmentId,
            type: suggestion.name,
            quantity: suggestion.quantity < 1 ? 1 : suggestion.quantity,
          ),
        ],
      );
      await _refresh();
      return null;
    } catch (error) {
      if (!isClosed) emit(state.copyWith(clearAddingId: true));
      return _failureOf(error);
    }
  }

  /// Reads the email again and fills the request with the machines it clearly
  /// names. Returns the failure when it didn't work, null when it did.
  Future<RequestsFailure?> autofill() async {
    if (state.busy) return null;
    emit(state.copyWith(isAutofilling: true));
    try {
      final request = await _autofill(state.request.id);
      if (isClosed) return null;
      emit(state.copyWith(request: request, isAutofilling: false));
      await start();
      return null;
    } catch (error) {
      if (!isClosed) emit(state.copyWith(isAutofilling: false));
      return _failureOf(error);
    }
  }

  /// Reloads the request after it changed, then its suggestions.
  Future<void> _refresh() async {
    final request = await _getRequest(state.request.id);
    if (isClosed) return;
    emit(
      state.copyWith(request: request ?? state.request, clearAddingId: true),
    );
    await start();
  }

  static RequestsFailure _failureOf(Object error) => error is RequestsFailure
      ? error
      : const RequestsFailure(RequestsFailureCode.unknown);
}
