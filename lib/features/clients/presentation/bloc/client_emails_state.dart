part of 'client_emails_cubit.dart';

enum ClientEmailsStatus { loading, ready, error }

class ClientEmailsState extends Equatable {
  final ClientEmailsStatus status;
  final List<ClientEmail> emails;
  final ClientsFailureCode? failure;

  /// The last add or remove that failed, for a message; cleared by the next
  /// one.
  final ClientsFailureCode? actionFailure;
  final bool isAdding;

  /// Ids of the entries being removed.
  final Set<String> removing;

  /// "Maybe" emails that became requests through the senders added so far.
  final int promoted;

  const ClientEmailsState({
    this.status = ClientEmailsStatus.loading,
    this.emails = const [],
    this.failure,
    this.actionFailure,
    this.isAdding = false,
    this.removing = const {},
    this.promoted = 0,
  });

  ClientEmailsState copyWith({
    ClientEmailsStatus? status,
    List<ClientEmail>? emails,
    ClientsFailureCode? failure,
    bool clearFailure = false,
    ClientsFailureCode? actionFailure,
    bool clearActionFailure = false,
    bool? isAdding,
    Set<String>? removing,
    int? promoted,
  }) {
    return ClientEmailsState(
      status: status ?? this.status,
      emails: emails ?? this.emails,
      failure: clearFailure ? null : (failure ?? this.failure),
      actionFailure: clearActionFailure ? null : (actionFailure ?? this.actionFailure),
      isAdding: isAdding ?? this.isAdding,
      removing: removing ?? this.removing,
      promoted: promoted ?? this.promoted,
    );
  }

  @override
  List<Object?> get props => [status, emails, failure, actionFailure, isAdding, removing, promoted];
}
