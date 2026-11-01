import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../core/shared/m_text_field.dart';
import '../../../../core/utils/validators.dart';
import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../machines/domain/entities/delivery.dart';
import '../../../machines/domain/usecases/get_machine_pdf.dart';
import '../../../machines/presentation/widgets/machine_widgets.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/delivery_email_cubit.dart';
import '../widgets/coordinator_labels.dart';
import '../widgets/delivery_certificate_tile.dart';

/// What the loading skeleton is drawn from; only its shape matters.
const _placeholder = DeliveryPreview(
  subject: 'Certificates for your equipment',
  certificates: [
    DeliveryCertificate(
      number: 'CERT-2026-0000',
      name: 'Overhead crane — 10 t',
      result: 'satisfactory',
      examinationDate: '2026-01-01',
      nextExaminationDate: '2026-07-01',
    ),
  ],
  canSend: true,
);

/// The last look before a job's certificates go to the client: the email as
/// the API proposes it — To, Cc (up to ten), subject and text — which the
/// coordinator can edit, and the certificates it carries, each openable as a
/// PDF. Send is off while the API names a blocker.
///
/// Pops with `true` once the email has gone out.
class DeliveryEmailPage extends StatelessWidget {
  final InspectionRequest request;

  const DeliveryEmailPage({super.key, required this.request});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<DeliveryEmailCubit>(param1: request.id)..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: _EmailView(request: request),
      ),
    );
  }
}

class _EmailView extends StatefulWidget {
  final InspectionRequest request;

  const _EmailView({required this.request});

  @override
  State<_EmailView> createState() => _EmailViewState();
}

class _EmailViewState extends State<_EmailView> {
  final _to = TextEditingController();
  final _cc = TextEditingController();
  final _subject = TextEditingController();
  final _body = TextEditingController();

  /// The To address is only called wrong once the coordinator has left the
  /// field, not while they are still typing it.
  bool _toTouched = false;
  bool _ccInvalid = false;

  @override
  void dispose() {
    _to.dispose();
    _cc.dispose();
    _subject.dispose();
    _body.dispose();
    super.dispose();
  }

  void _fill(DeliveryEmailState state) {
    _to.text = state.to;
    _subject.text = state.subject;
    _body.text = state.body;
  }

  void _addCc() {
    final added = context.read<DeliveryEmailCubit>().addCc(_cc.text);
    setState(() => _ccInvalid = !added);
    if (added) _cc.clear();
  }

  Future<void> _confirmSend() async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<DeliveryEmailCubit>();
    final confirmed = await showAdaptiveDialog<bool>(
      context: context,
      builder: (context) => AlertDialog.adaptive(
        title: Text(t.confirmSendTitle),
        content: Text(t.confirmSendMessage(cubit.state.to.trim())),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(false), child: Text(t.cancel)),
          TextButton(onPressed: () => Navigator.of(context).pop(true), child: Text(t.sendToClientAction)),
        ],
      ),
    );
    if (confirmed == true) await cubit.send();
  }

  Future<void> _previewPdf(DeliveryCertificate certificate) {
    final t = AppLocalizations.of(context)!;
    final itemId = certificate.requestItemId!;
    final number = certificate.number.isEmpty ? widget.request.id : certificate.number;
    return PdfPreviewPage.open(
      context,
      title: t.certificateTitle,
      subtitle: number,
      fileName: '$number.pdf',
      build: () => getIt<GetMachinePdf>()(widget.request.id, itemId),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final request = widget.request;

    return MultiBlocListener(
      listeners: [
        BlocListener<DeliveryEmailCubit, DeliveryEmailState>(
          listenWhen: (previous, current) =>
              previous.status != current.status && current.status == DeliveryEmailStatus.ready,
          listener: (context, state) => _fill(state),
        ),
        BlocListener<DeliveryEmailCubit, DeliveryEmailState>(
          listenWhen: (previous, current) => previous.actionSeq != current.actionSeq,
          listener: (context, state) {
            if (state.lastActionSuccess) {
              MToast.showSuccess(
                message: state.lastResult?.emailSent == true
                    ? t.certificateSentMessage
                    : t.certificateDeliveredNoEmailMessage,
              );
              Navigator.of(context).pop(true);
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
            MachinesHeader(
              eyebrow: request.clientName.isEmpty ? request.id : request.clientName,
              title: t.emailPreviewTitle,
            ),
            const SizedBox(height: 12),
            Expanded(
              child: BlocBuilder<DeliveryEmailCubit, DeliveryEmailState>(
                buildWhen: (previous, current) =>
                    previous.status != current.status ||
                    previous.preview != current.preview ||
                    previous.cc != current.cc ||
                    previous.isSending != current.isSending ||
                    previous.to != current.to,
                builder: (context, state) {
                  if (state.status == DeliveryEmailStatus.error) {
                    return Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            MNotice(type: MNoticeType.error, message: state.loadFailure!.message(t)),
                            const SizedBox(height: 16),
                            OutlinedButton(
                              onPressed: context.read<DeliveryEmailCubit>().retry,
                              child: Text(t.retry),
                            ),
                          ],
                        ),
                      ),
                    );
                  }

                  // While loading, the form is drawn from a placeholder email
                  // and shimmers.
                  final loading = state.status == DeliveryEmailStatus.loading;
                  final cubit = context.read<DeliveryEmailCubit>();
                  final preview = state.preview ?? _placeholder;
                  final ccFull = state.cc.length >= DeliveryEmailCubit.maxCc;
                  return Skeletonizer(
                    enabled: loading,
                    child: ListView(
                    physics: loading ? const NeverScrollableScrollPhysics() : null,
                    keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
                    padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
                    children: [
                      if (preview.blockers.isNotEmpty) ...[
                        MNotice(
                          type: MNoticeType.error,
                          title: t.emailCannotSendTitle,
                          message: preview.blockers.join('\n'),
                        ),
                        const SizedBox(height: 12),
                      ],
                      MCard(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Focus(
                              onFocusChange: (focused) {
                                if (!focused && !_toTouched) setState(() => _toTouched = true);
                              },
                              child: MTextField(
                                controller: _to,
                                onChanged: cubit.setTo,
                                enabled: !state.isSending,
                                label: t.emailToLabel,
                                prefixIcon: Icons.mail_outline_rounded,
                                keyboardType: TextInputType.emailAddress,
                                hintText: t.emailHint,
                                errorText: _toTouched && state.to.trim().isNotEmpty && !Validators.isEmail(state.to)
                                    ? t.emailInvalidAddress
                                    : null,
                              ),
                            ),
                            const SizedBox(height: 16),
                            Text(t.emailCcLabel, style: AppTextStyles.badge),
                            if (state.cc.isNotEmpty) ...[
                              const SizedBox(height: 8),
                              Wrap(
                                spacing: 8,
                                runSpacing: 8,
                                children: [
                                  for (final address in state.cc)
                                    InputChip(
                                      label: Text(address, style: AppTextStyles.caption),
                                      onDeleted: state.isSending ? null : () => cubit.removeCc(address),
                                      backgroundColor: AppColours.surfaceMuted,
                                      side: BorderSide.none,
                                    ),
                                ],
                              ),
                            ],
                            const SizedBox(height: 8),
                            MTextField(
                              controller: _cc,
                              onChanged: (_) {
                                if (_ccInvalid) setState(() => _ccInvalid = false);
                              },
                              onSubmitted: (_) => _addCc(),
                              enabled: !state.isSending && !ccFull,
                              prefixIcon: Icons.person_add_alt_1_outlined,
                              keyboardType: TextInputType.emailAddress,
                              textInputAction: TextInputAction.done,
                              hintText: t.emailCcHint,
                              errorText: _ccInvalid ? t.emailInvalidAddress : null,
                              trailing: IconButton(
                                onPressed: state.isSending || ccFull ? null : _addCc,
                                iconSize: 22,
                                color: AppColours.primaryColor,
                                icon: const Icon(Icons.add_rounded),
                                tooltip: t.emailCcAddAction,
                              ),
                            ),
                            if (ccFull) ...[
                              const SizedBox(height: 6),
                              Text(t.emailCcLimit(DeliveryEmailCubit.maxCc), style: AppTextStyles.caption),
                            ],
                            const SizedBox(height: 16),
                            MTextField(
                              controller: _subject,
                              onChanged: cubit.setSubject,
                              enabled: !state.isSending,
                              label: t.emailSubjectLabel,
                              prefixIcon: Icons.subject_rounded,
                              textInputAction: TextInputAction.next,
                            ),
                            const SizedBox(height: 16),
                            Text(t.emailBodyLabel, style: AppTextStyles.badge),
                            const SizedBox(height: 8),
                            TextField(
                              controller: _body,
                              onChanged: cubit.setBody,
                              enabled: !state.isSending,
                              minLines: 8,
                              maxLines: 16,
                              keyboardType: TextInputType.multiline,
                              textInputAction: TextInputAction.newline,
                              style: AppTextStyles.body,
                              decoration: InputDecoration(
                                border: OutlineInputBorder(borderRadius: BorderRadius.circular(14)),
                                focusedBorder: OutlineInputBorder(
                                  borderRadius: BorderRadius.circular(14),
                                  borderSide: const BorderSide(color: AppColours.primaryColor, width: 2),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                      if (preview.certificates.isNotEmpty) ...[
                        const SizedBox(height: 20),
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 4),
                          child: Text(t.emailAttachmentsTitle, style: AppTextStyles.sectionTitle),
                        ),
                        const SizedBox(height: 12),
                        MCard(
                          child: Column(
                            children: [
                              for (final (index, certificate) in preview.certificates.indexed) ...[
                                if (index > 0) const Divider(height: 24, color: AppColours.borderSubtle),
                                DeliveryCertificateTile(
                                  number: certificate.number,
                                  name: certificate.name,
                                  result: certificate.result,
                                  examinationDate: certificate.examinationDate,
                                  nextExaminationDate: certificate.nextExaminationDate,
                                  onPreview: certificate.requestItemId == null
                                      ? null
                                      : () => _previewPdf(certificate),
                                ),
                              ],
                            ],
                          ),
                        ),
                      ],
                    ],
                    ),
                  );
                },
              ),
            ),
            BlocBuilder<DeliveryEmailCubit, DeliveryEmailState>(
              builder: (context, state) {
                if (state.status != DeliveryEmailStatus.ready) return const SizedBox.shrink();
                return Container(
                  padding: const EdgeInsets.fromLTRB(20, 16, 20, 16),
                  decoration: const BoxDecoration(
                    color: Colors.white,
                    border: Border(top: BorderSide(color: AppColours.borderSubtle)),
                  ),
                  child: SafeArea(
                    top: false,
                    minimum: const EdgeInsets.only(bottom: 6),
                    child: MPrimaryButton(
                      label: t.sendToClientAction,
                      loading: state.isSending,
                      onPressed: state.canSend ? _confirmSend : null,
                    ),
                  ),
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}
