import 'package:equatable/equatable.dart';

enum StateType {
  loading,
  error,
  success,
  loadingPopUp,
  errorPopUp,
  successPopUp,
  none,
  delete,
}

class FlowState extends Equatable {
  final String? message;
  final StateType type;
  final dynamic data;

  const FlowState({this.message = '', this.type = StateType.none, this.data});

  FlowState copyWith({String? message, StateType? type, dynamic data}) {
    return FlowState(
      data: data ?? this.data,
      message: message ?? this.message,
      type: type ?? this.type,
    );
  }

  @override
  List<Object?> get props => [message, data, type];
}
