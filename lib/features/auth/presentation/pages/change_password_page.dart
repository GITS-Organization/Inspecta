import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/builder/flow_state.dart';
import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_password_field.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../core/utils/validators.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../bloc/change_password_cubit.dart';
import '../widgets/auth_labels.dart';

/// Profile > Change password: the signed-in user picks a new password.
/// Needs the current one. This device stays signed in; the API signs the
/// other devices out.
class ChangePasswordPage extends StatelessWidget {
  const ChangePasswordPage({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<ChangePasswordCubit>(),
      child: const _ChangePasswordView(),
    );
  }
}

class _ChangePasswordView extends StatefulWidget {
  const _ChangePasswordView();

  @override
  State<_ChangePasswordView> createState() => _ChangePasswordViewState();
}

class _ChangePasswordViewState extends State<_ChangePasswordView> {
  final _current = TextEditingController();
  final _new = TextEditingController();
  final _confirm = TextEditingController();
  bool _submitted = false;
  String? _currentError;
  String? _newError;
  String? _confirmError;

  @override
  void dispose() {
    _current.dispose();
    _new.dispose();
    _confirm.dispose();
    super.dispose();
  }

  void _validate(AppLocalizations t) {
    _currentError = _current.text.isEmpty ? t.errorPasswordRequired : null;
    _newError = _new.text.isEmpty
        ? t.errorPasswordRequired
        : !Validators.isStrongPassword(_new.text)
        ? t.errorWeakPassword
        : null;
    _confirmError = _confirm.text != _new.text
        ? t.errorPasswordsDoNotMatch
        : null;
  }

  void _submit() {
    final t = AppLocalizations.of(context)!;
    setState(() {
      _submitted = true;
      _validate(t);
    });
    if (_currentError != null || _newError != null || _confirmError != null) {
      return;
    }
    FocusScope.of(context).unfocus();
    context.read<ChangePasswordCubit>().submit(
      currentPassword: _current.text,
      newPassword: _new.text,
    );
  }

  void _revalidate(AppLocalizations t) {
    if (_submitted) setState(() => _validate(t));
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Scaffold(
      backgroundColor: Colors.white,
      body: SafeArea(
        child: BlocConsumer<ChangePasswordCubit, FlowState>(
          listener: (context, state) {
            // A saved password must not stay on screen.
            if (state.type == StateType.success) {
              _current.clear();
              _new.clear();
              _confirm.clear();
              setState(() => _submitted = false);
            }
          },
          builder: (context, state) {
            final cubit = context.read<ChangePasswordCubit>();
            final failure = cubit.failure;
            return SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(28, 16, 28, 28),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Align(
                    alignment: AlignmentDirectional.centerStart,
                    child: MBackButton(),
                  ),
                  const SizedBox(height: 22),
                  Container(
                    width: 72,
                    height: 72,
                    decoration: BoxDecoration(
                      color: AppColours.primarySoft,
                      borderRadius: BorderRadius.circular(24),
                    ),
                    child: const Icon(
                      Icons.lock_outline_rounded,
                      size: 34,
                      color: AppColours.primaryDark,
                    ),
                  ),
                  const SizedBox(height: 22),
                  Text(
                    t.changePasswordTitle,
                    style: AppTextStyles.pageTitle.copyWith(fontSize: 28),
                  ),
                  const SizedBox(height: 8),
                  Text(t.changePasswordSubtitle, style: AppTextStyles.body),
                  const SizedBox(height: 22),
                  MPasswordField(
                    controller: _current,
                    label: t.currentPasswordLabel,
                    enabled: !cubit.isSaving,
                    errorText: _currentError,
                    onChanged: (_) => _revalidate(t),
                  ),
                  const SizedBox(height: 16),
                  MPasswordField(
                    controller: _new,
                    label: t.newPasswordLabel,
                    hintText: t.passwordRulesHint,
                    enabled: !cubit.isSaving,
                    errorText: _newError,
                    onChanged: (_) => _revalidate(t),
                  ),
                  const SizedBox(height: 16),
                  MPasswordField(
                    controller: _confirm,
                    label: t.confirmPasswordLabel,
                    enabled: !cubit.isSaving,
                    errorText: _confirmError,
                    onChanged: (_) => _revalidate(t),
                    onSubmitted: (_) => _submit(),
                  ),
                  const SizedBox(height: 22),
                  MPrimaryButton(
                    label: t.savePassword,
                    loading: cubit.isSaving,
                    onPressed: _submit,
                  ),
                  if (cubit.isSaved) ...[
                    const SizedBox(height: 22),
                    MNotice(
                      type: MNoticeType.success,
                      title: t.passwordChangedTitle,
                      message: t.passwordChangedMessage,
                    ),
                  ],
                  if (failure != null) ...[
                    const SizedBox(height: 22),
                    MNotice(
                      type: MNoticeType.error,
                      message: failure.message(t),
                    ),
                  ],
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}
