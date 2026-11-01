import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../domain/entities/client.dart';
import '../../domain/entities/clients_failure.dart';
import '../../domain/usecases/add_client_email.dart';
import '../../domain/usecases/get_client_emails.dart';
import '../../domain/usecases/remove_client_email.dart';

part 'client_emails_state.dart';

/// Backs a client's "Emails" screen: the addresses and domains whose emails
/// become the client's requests — listed, added and removed.
///
/// Adding one can turn waiting "maybe" emails into requests; [promoted] counts
/// them over the session so the screen that opened this one knows to reload
/// its lists.
class ClientEmailsCubit extends Cubit<ClientEmailsState> {
  final Client client;
  final GetClientEmails _getEmails;
  final AddClientEmail _addEmail;
  final RemoveClientEmail _removeEmail;

  ClientEmailsCubit({
    required this.client,
    required GetClientEmails getClientEmails,
    required AddClientEmail addClientEmail,
    required RemoveClientEmail removeClientEmail,
  }) : _getEmails = getClientEmails,
       _addEmail = addClientEmail,
       _removeEmail = removeClientEmail,
       super(const ClientEmailsState());

  Future<void> start() async {
    try {
      final emails = await _getEmails(client.id);
      if (isClosed) return;
      emit(state.copyWith(status: ClientEmailsStatus.ready, emails: emails, clearFailure: true));
    } catch (error) {
      if (isClosed) return;
      emit(state.copyWith(status: ClientEmailsStatus.error, failure: _codeOf(error)));
    }
  }

  Future<void> retry() {
    emit(state.copyWith(status: ClientEmailsStatus.loading, clearFailure: true));
    return start();
  }

  /// Registers [pattern] (an address or a domain). Returns the new entry, or
  /// null when it failed — [ClientEmailsState.actionFailure] says why.
  Future<ClientEmail?> add(String pattern) async {
    if (state.isAdding) return null;
    emit(state.copyWith(isAdding: true, clearActionFailure: true));
    try {
      final added = await _addEmail(client.id, pattern);
      if (isClosed) return added;
      emit(
        state.copyWith(
          isAdding: false,
          emails: [...state.emails.where((e) => e.id != added.id), added],
          promoted: state.promoted + added.promotedRequests,
        ),
      );
      return added;
    } catch (error) {
      if (!isClosed) emit(state.copyWith(isAdding: false, actionFailure: _codeOf(error)));
      return null;
    }
  }

  /// Removes [email]. Returns whether it worked.
  Future<bool> remove(ClientEmail email) async {
    if (state.removing.contains(email.id)) return false;
    emit(state.copyWith(removing: {...state.removing, email.id}, clearActionFailure: true));
    try {
      await _removeEmail(client.id, email.id);
      if (isClosed) return true;
      emit(
        state.copyWith(
          emails: [...state.emails.where((e) => e.id != email.id)],
          removing: {...state.removing}..remove(email.id),
        ),
      );
      return true;
    } catch (error) {
      if (!isClosed) {
        emit(
          state.copyWith(
            removing: {...state.removing}..remove(email.id),
            actionFailure: _codeOf(error),
          ),
        );
      }
      return false;
    }
  }

  static ClientsFailureCode _codeOf(Object error) =>
      error is ClientsFailure ? error.code : ClientsFailureCode.unknown;
}
