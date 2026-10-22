import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_text_field.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../core/widgets/network_painter.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/presentation/pdf/certificate_pdf_preview.dart';
import '../../../certificate/presentation/widgets/certificate_labels.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/presentation/widgets/job_stepper.dart';
import '../../../requests/presentation/widgets/status_chip.dart';
import '../../../technical_manager/presentation/widgets/review_summary_card.dart';
import '../bloc/send_certificate_cubit.dart';

/// The coordinator's last step on a job: the technical manager has approved
/// and signed the certificate, and the coordinator sends it to the client —
/// downloading the signed PDF, picking the address, and marking it as sent.
class SendCertificatePage extends StatelessWidget {
  final InspectionRequest request;

  const SendCertificatePage({super.key, required this.request});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<SendCertificateCubit>(param1: request)..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: _SendView(request: request),
      ),
    );
  }
}

class _SendView extends StatefulWidget {
  final InspectionRequest request;

  const _SendView({required this.request});

  @override
  State<_SendView> createState() => _SendViewState();
}

class _SendViewState extends State<_SendView> {
  final _email = TextEditingController();

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  Future<void> _confirmSend() async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<SendCertificateCubit>();
    final confirmed = await showAdaptiveDialog<bool>(
      context: context,
      builder: (context) => AlertDialog.adaptive(
        title: Text(t.confirmSendTitle),
        content: Text(t.confirmSendMessage(cubit.state.email.trim())),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(t.cancel),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(t.sendToClientAction),
          ),
        ],
      ),
    );
    if (confirmed == true) await cubit.send();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final request = widget.request;
    return MultiBlocListener(
      listeners: [
        BlocListener<SendCertificateCubit, SendCertificateState>(
          listenWhen: (previous, current) =>
              previous.suggestedEmail != current.suggestedEmail,
          listener: (context, state) {
            if (_email.text.isEmpty && state.suggestedEmail != null) {
              _email.text = state.suggestedEmail!;
            }
          },
        ),
        BlocListener<SendCertificateCubit, SendCertificateState>(
          listenWhen: (previous, current) =>
              previous.actionSeq != current.actionSeq,
          listener: (context, state) {
            if (state.lastActionSuccess) {
              MToast.showSuccess(message: t.certificateSentMessage);
              Navigator.of(context).pop();
            } else if (state.lastActionFailure != null) {
              MToast.showError(message: state.lastActionFailure!.message(t));
            }
          },
        ),
      ],
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: Column(
          children: [
            _Header(request: request),
            Expanded(
              child: BlocBuilder<SendCertificateCubit, SendCertificateState>(
                builder: (context, state) {
                  switch (state.status) {
                    case SendCertificateStatus.loading:
                      return Loading.loader(context);
                    case SendCertificateStatus.error:
                    case SendCertificateStatus.notFound:
                      return Center(
                        child: Padding(
                          padding: const EdgeInsets.all(24),
                          child: MNotice(
                            type: MNoticeType.error,
                            message: t.certificateNotFound,
                          ),
                        ),
                      );
                    case SendCertificateStatus.ready:
                      break;
                  }
                  final certificate = state.certificate;
                  if (certificate == null) return const SizedBox.shrink();
                  return ListView(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
                    children: [
                      _CertificatePass(
                        status: request.status,
                        code: certificate.verificationCode,
                        onViewPdf: () => _viewPdf(certificate),
                      ),
                      const SizedBox(height: 12),
                      ReviewSummaryCard(
                        request: request,
                        certificate: certificate,
                      ),
                      const SizedBox(height: 12),
                      MCard(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(
                              t.sendSignedPdfToLabel,
                              style: AppTextStyles.sectionTitle.copyWith(
                                fontSize: 14,
                              ),
                            ),
                            const SizedBox(height: 12),
                            MTextField(
                              controller: _email,
                              onChanged: context
                                  .read<SendCertificateCubit>()
                                  .setEmail,
                              enabled: !state.isSending,
                              prefixIcon: Icons.mail_outline_rounded,
                              keyboardType: TextInputType.emailAddress,
                              hintText: t.emailHint,
                            ),
                            const SizedBox(height: 12),
                            Text(
                              t.sendCertificateHint,
                              style: AppTextStyles.caption,
                            ),
                          ],
                        ),
                      ),
                    ],
                  );
                },
              ),
            ),
            Container(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 16),
              decoration: const BoxDecoration(
                color: Colors.white,
                border: Border(top: BorderSide(color: AppColours.borderSubtle)),
              ),
              child: SafeArea(
                top: false,
                minimum: const EdgeInsets.only(bottom: 6),
                child: BlocBuilder<SendCertificateCubit, SendCertificateState>(
                  builder: (context, state) {
                    final enabled = state.canSend && !state.isSending;
                    return SizedBox(
                      height: 54,
                      width: double.infinity,
                      child: FilledButton(
                        onPressed: enabled ? _confirmSend : null,
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColours.primaryColor,
                          disabledBackgroundColor: state.isSending
                              ? AppColours.primaryColor
                              : AppColours.primaryColor.withValues(alpha: 0.4),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(14),
                          ),
                        ),
                        child: state.isSending
                            ? const SizedBox.square(
                                dimension: 20,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2.5,
                                  color: Colors.white,
                                ),
                              )
                            : Row(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  const Icon(
                                    Icons.send_rounded,
                                    size: 18,
                                    color: Colors.white,
                                  ),
                                  const SizedBox(width: 8),
                                  Text(
                                    t.sendToClientAction,
                                    style: AppTextStyles.buttonLabel,
                                  ),
                                ],
                              ),
                      ),
                    );
                  },
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _viewPdf(Certificate certificate) {
    return previewCertificatePdf(
      context,
      request: widget.request,
      certificate: certificate,
    );
  }
}

/// The certificate as a navy "pass": the approved status, the verification
/// code the technical manager's approval issued (what the QR on the PDF
/// and on the dashboard carries) — or a note when the certificate predates
/// QR codes — and the button to preview the final signed PDF.
class _CertificatePass extends StatelessWidget {
  final JobStatus status;
  final String? code;
  final VoidCallback onViewPdf;

  const _CertificatePass({
    required this.status,
    required this.code,
    required this.onViewPdf,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final issued = code != null;

    return ClipRRect(
      borderRadius: BorderRadius.circular(24),
      child: DecoratedBox(
        decoration: const BoxDecoration(color: AppColours.navy),
        child: Stack(
          children: [
            const Positioned.fill(
              child: IgnorePointer(child: AnimatedNetwork(nodes: 10)),
            ),
            Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 6,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(999),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(
                          Icons.verified_rounded,
                          size: 14,
                          color: AppColours.successOnNavy,
                        ),
                        const SizedBox(width: 6),
                        Text(
                          status.label(t),
                          style: AppTextStyles.badge.copyWith(
                            color: AppColours.successOnNavy,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),
                  Row(
                    children: [
                      Container(
                        width: 56,
                        height: 56,
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(14),
                        ),
                        child: Icon(
                          Icons.qr_code_2_rounded,
                          size: 36,
                          color: issued ? AppColours.navy : AppColours.inkMuted,
                        ),
                      ),
                      const SizedBox(width: 14),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(
                              t.verificationCodeLabel,
                              style: AppTextStyles.caption.copyWith(
                                fontSize: 12,
                                height: 16 / 12,
                                color: AppColours.onNavyMuted,
                              ),
                            ),
                            const SizedBox(height: 2),
                            Text(
                              code ?? t.noVerificationCodeHint,
                              style: issued
                                  ? const TextStyle(
                                      fontFamily: 'monospace',
                                      fontSize: 20,
                                      height: 28 / 20,
                                      fontWeight: FontWeight.w700,
                                      letterSpacing: 1,
                                      color: Colors.white,
                                    )
                                  : AppTextStyles.subtitle.copyWith(
                                      fontSize: 13,
                                      color: AppColours.amberOnPrimary,
                                    ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 20),
                  SizedBox(
                    height: 48,
                    width: double.infinity,
                    child: FilledButton.icon(
                      onPressed: onViewPdf,
                      icon: const Icon(Icons.picture_as_pdf_outlined, size: 18),
                      label: Text(t.previewFinalPdfAction),
                      style: FilledButton.styleFrom(
                        backgroundColor: Colors.white,
                        foregroundColor: AppColours.navy,
                        textStyle: AppTextStyles.buttonLabel,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(14),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The banner: back button, certificate number and client, the title, and
/// the job's progress row — which, with the certificate approved, shows
/// everything done up to Certify and only "Sent" left.
class _Header extends StatelessWidget {
  final InspectionRequest request;

  const _Header({required this.request});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AppBanner(
      topInset: MediaQuery.paddingOf(context).top,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              const MBackButton(),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      '${certNumberFor(request)} · ${request.clientName}',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.caption.copyWith(
                        fontWeight: FontWeight.w600,
                        letterSpacing: 0.24,
                        height: 16 / 12,
                        fontSize: 12,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      t.sendCertificateTitle,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.sectionTitle.copyWith(
                        fontSize: 22,
                        height: 28 / 22,
                        letterSpacing: -0.22,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 18),
          JobStepper(status: request.status),
        ],
      ),
    );
  }
}
