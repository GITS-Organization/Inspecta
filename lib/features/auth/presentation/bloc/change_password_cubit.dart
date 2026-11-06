import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/builder/flow_state.dart';
import '../../domain/entities/auth_failure.dart';
import '../../domain/usecases/change_password.dart';

/// Changing the signed-in user's own password. States: [StateType.loading]
/// while saving, [StateType.success] once saved, [StateType.error] with an
/// [AuthFailureCode] in `data`.
class ChangePasswordCubit extends Cubit<FlowState> {
  final ChangePassword _changePassword;

  ChangePasswordCubit(this._changePassword) : super(const FlowState());

  bool get isSaving => state.type == StateType.loading;

  bool get isSaved => state.type == StateType.success;

  AuthFailureCode? get failure =>
      state.type == StateType.error ? state.data as AuthFailureCode? : null;

  Future<void> submit({
    required String currentPassword,
    required String newPassword,
  }) async {
    if (isSaving) return;
    emit(const FlowState(type: StateType.loading));
    try {
      await _changePassword(
        currentPassword: currentPassword,
        newPassword: newPassword,
      );
      if (!isClosed) emit(const FlowState(type: StateType.success));
    } on AuthFailure catch (f) {
      if (!isClosed) emit(FlowState(type: StateType.error, data: f.code));
    }
  }
}
