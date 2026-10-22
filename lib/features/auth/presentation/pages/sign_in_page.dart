import 'dart:ui' show ImageFilter;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/builder/flow_state.dart';
import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_password_field.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../core/shared/m_text_field.dart';
import '../../../../core/utils/validators.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../bloc/auth_cubit.dart';
import '../bloc/sign_in_cubit.dart';
import '../widgets/auth_labels.dart';
import 'forgot_password_page.dart';
import 'login_hero.dart';

/// US-01.1 / US-01.2: email + password sign in with "Keep me signed in".
class SignInPage extends StatelessWidget {
  const SignInPage({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<SignInCubit>(),
      child: const _SignInView(),
    );
  }
}

class _SignInView extends StatefulWidget {
  const _SignInView();

  @override
  State<_SignInView> createState() => _SignInViewState();
}

class _SignInViewState extends State<_SignInView> {
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _keepSignedIn = true;

  /// Field errors appear only after the first submit, then update live.
  bool _submitted = false;
  String? _emailError;
  String? _passwordError;

  @override
  void initState() {
    super.initState();
    final authState = context.read<AuthCubit>().state;
    if (authState is Unauthenticated && authState.reason != null) {
      context.read<SignInCubit>().showFailure(authState.reason!);
    }
  }

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  void _validate(AppLocalizations t) {
    final email = _emailController.text.trim();
    _emailError = email.isEmpty
        ? t.errorEmailRequired
        : !Validators.isEmail(email)
        ? t.errorEmailInvalid
        : null;
    _passwordError = _passwordController.text.isEmpty
        ? t.errorPasswordRequired
        : null;
  }

  void _onChanged(String _) {
    context.read<SignInCubit>().clearFailure();
    if (_submitted) setState(() => _validate(AppLocalizations.of(context)!));
  }

  void _submit() {
    final t = AppLocalizations.of(context)!;
    setState(() {
      _submitted = true;
      _validate(t);
    });
    if (_emailError != null || _passwordError != null) return;

    FocusScope.of(context).unfocus();
    TextInput.finishAutofillContext();
    context.read<SignInCubit>().submit(
      email: _emailController.text,
      password: _passwordController.text,
      keepSignedIn: _keepSignedIn,
    );
  }

  void _openForgotPassword() {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) =>
            ForgotPasswordPage(initialEmail: _emailController.text.trim()),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return BlocListener<AuthCubit, AuthState>(
      listenWhen: (_, current) =>
          current is Unauthenticated && current.reason != null,
      listener: (context, state) => context.read<SignInCubit>().showFailure(
        (state as Unauthenticated).reason!,
      ),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.light,
        child: Scaffold(
          backgroundColor: AppColours.navy,
          body: LoginHero(
            child: CustomScrollView(
              slivers: [
                SliverFillRemaining(
                  hasScrollBody: false,
                  child: Column(
                    children: [
                      SafeArea(
                        bottom: false,
                        minimum: const EdgeInsets.only(top: 16),
                        child: const Padding(
                          padding: EdgeInsets.fromLTRB(28, 24, 28, 28),
                          child: LoginIntro(),
                        ),
                      ),
                      const Spacer(),

                      Container(
                        margin: const EdgeInsets.symmetric(horizontal: 30),
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(28),
                          boxShadow: [
                            BoxShadow(
                              color: Colors.black.withValues(alpha: 0.28),
                              blurRadius: 40,
                              offset: const Offset(0, 16),
                            ),
                          ],
                        ),
                        child: ClipRRect(
                          borderRadius: BorderRadius.circular(28),
                          child: BackdropFilter(
                            filter: ImageFilter.blur(sigmaX: 14, sigmaY: 14),
                            child: ColoredBox(
                              color: Colors.white.withValues(alpha: 0.88),
                              child: _buildForm(t),
                            ),
                          ),
                        ),
                      ),
                      SafeArea(
                        top: false,
                        minimum: const EdgeInsets.fromLTRB(28, 20, 28, 24),
                        child: Text(
                          t.noAccount,
                          textAlign: TextAlign.center,
                          style: AppTextStyles.caption.copyWith(
                            color: AppColours.onNavyMuted,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildForm(AppLocalizations t) {
    return BlocBuilder<SignInCubit, FlowState>(
      builder: (context, state) {
        final cubit = context.read<SignInCubit>();
        final submitting = cubit.isSubmitting;
        final failure = cubit.failure;

        return Padding(
          padding: const EdgeInsets.fromLTRB(24, 36, 24, 36),
          child: AutofillGroup(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  t.signInTitle,
                  style: AppTextStyles.sectionTitle.copyWith(
                    fontSize: 26,
                    height: 32 / 26,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  t.signInSubtitle,
                  style: AppTextStyles.caption.copyWith(
                    fontSize: 14,
                    height: 20 / 14,
                  ),
                ),
                const SizedBox(height: 32),
                if (failure != null) ...[
                  MNotice(type: MNoticeType.error, message: failure.message(t)),
                  const SizedBox(height: 18),
                ],

                MTextField(
                  controller: _emailController,
                  label: t.workEmail,
                  hintText: t.emailHint,
                  prefixIcon: Icons.mail_outline_rounded,
                  keyboardType: TextInputType.emailAddress,
                  textInputAction: TextInputAction.next,
                  autofillHints: const [
                    AutofillHints.email,
                    AutofillHints.username,
                  ],
                  enabled: !submitting,
                  errorText: _emailError,
                  onChanged: _onChanged,
                ),
                const SizedBox(height: 24),
                MPasswordField(
                  controller: _passwordController,
                  label: t.password,
                  hintText: '********',
                  enabled: !submitting,
                  errorText: _passwordError,
                  onChanged: _onChanged,
                  onSubmitted: (_) => _submit(),
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(
                      child: _KeepSignedInCheckbox(
                        value: _keepSignedIn,
                        label: t.keepMeSignedIn,
                        onChanged: submitting
                            ? null
                            : (v) => setState(() => _keepSignedIn = v),
                      ),
                    ),
                    TextButton(
                      onPressed: submitting ? null : _openForgotPassword,
                      style: TextButton.styleFrom(
                        foregroundColor: AppColours.primaryDark,
                        textStyle: AppTextStyles.link,
                        minimumSize: const Size(44, 44),
                        padding: const EdgeInsets.symmetric(horizontal: 4),
                      ),
                      child: Text(t.forgotPassword),
                    ),
                  ],
                ),
                const SizedBox(height: 28),

                MPrimaryButton(
                  label: t.signIn,
                  loading: submitting,
                  onPressed: _submit,
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _KeepSignedInCheckbox extends StatelessWidget {
  final bool value;
  final String label;
  final ValueChanged<bool>? onChanged;

  const _KeepSignedInCheckbox({
    required this.value,
    required this.label,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return MergeSemantics(
      child: InkWell(
        onTap: onChanged == null ? null : () => onChanged!(!value),
        borderRadius: BorderRadius.circular(10),
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 44),
          child: Row(
            children: [
              Checkbox(
                value: value,
                onChanged: onChanged == null ? null : (v) => onChanged!(v!),
                activeColor: AppColours.primaryColor,
                materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                visualDensity: VisualDensity.compact,
              ),
              const SizedBox(width: 6),
              Flexible(
                child: Text(
                  label,
                  style: AppTextStyles.subtitle.copyWith(
                    color: AppColours.inkBody,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
