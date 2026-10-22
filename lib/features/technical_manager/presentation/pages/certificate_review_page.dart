import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/bloc/auth_cubit.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/presentation/pdf/certificate_pdf_preview.dart';
import '../../../certificate/presentation/widgets/certificate_labels.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../bloc/review_detail_cubit.dart';
import '../widgets/review_comments_field.dart';
import '../widgets/review_detail_header.dart';
import '../widgets/review_footer.dart';
import '../widgets/review_summary_card.dart';
import '../widgets/signature_card.dart';

/// Feature 06's review screen: the reviewer reads the summary, adds
/// comments, signs, and either approves (which issues the certificate's QR
/// code and hands it to the coordinator to send to the client) or returns the
/// certificate to its inspector.
class CertificateReviewPage extends StatelessWidget {
  final InspectionRequest request;
  final String? inspectorName;

  const CertificateReviewPage({super.key, required this.request, this.inspectorName});

  @override
  Widget build(BuildContext context) {
    final reviewer = context.read<AuthCubit>().user;
    return BlocProvider(
      create: (_) => getIt<ReviewDetailCubit>(param1: request, param2: reviewer?.name ?? '')..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: _ReviewView(request: request, inspectorName: inspectorName),
      ),
    );
  }
}

class _ReviewView extends StatefulWidget {
  final InspectionRequest request;
  final String? inspectorName;

  const _ReviewView({required this.request, this.inspectorName});

  @override
  State<_ReviewView> createState() => _ReviewViewState();
}

class _ReviewViewState extends State<_ReviewView> {
  final _comments = TextEditingController();

  @override
  void dispose() {
    _comments.dispose();
    super.dispose();
  }

  Future<void> _viewPdf(Certificate certificate) {
    return previewCertificatePdf(context, request: widget.request, certificate: certificate);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<ReviewDetailCubit>();
    return MultiBlocListener(
      listeners: [
        BlocListener<ReviewDetailCubit, ReviewDetailState>(
          listenWhen: (previous, current) => previous.actionSeq != current.actionSeq,
          listener: (context, state) {
            if (state.lastActionSuccess) {
              MToast.showSuccess(
                message: state.lastActionApproved ? t.certificateApprovedMessage : t.certificateReturnedMessage,
              );
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
            BlocSelector<ReviewDetailCubit, ReviewDetailState, DateTime?>(
              selector: (state) => state.certificate?.submittedAt,
              builder: (context, submittedAt) => ReviewDetailHeader(
                request: widget.request,
                inspectorName: widget.inspectorName,
                submittedAt: submittedAt,
              ),
            ),
            Expanded(
              child: BlocBuilder<ReviewDetailCubit, ReviewDetailState>(
                buildWhen: (previous, current) =>
                    previous.status != current.status ||
                    previous.certificate != current.certificate ||
                    previous.strokes != current.strokes ||
                    previous.savedStrokes != current.savedStrokes ||
                    previous.rememberSignature != current.rememberSignature ||
                    previous.isBusy != current.isBusy ||
                    previous.canApprove != current.canApprove,
                builder: (context, state) {
                  final certificate = state.certificate;
                  switch (state.status) {
                    case ReviewDetailStatus.loading:
                      return Loading.loader(context);
                    case ReviewDetailStatus.error:
                    case ReviewDetailStatus.notFound:
                      return Center(
                        child: Padding(
                          padding: const EdgeInsets.all(24),
                          child: MNotice(type: MNoticeType.error, message: t.certificateNotFound),
                        ),
                      );
                    case ReviewDetailStatus.ready:
                      break;
                  }
                  if (certificate == null) return const SizedBox.shrink();
                  return ListView(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
                    children: [
                      ReviewSummaryCard(
                        request: widget.request,
                        certificate: certificate,
                        onViewPdf: () => _viewPdf(certificate),
                      ),
                      const SizedBox(height: 12),
                      ReviewCommentsField(
                        controller: _comments,
                        onChanged: cubit.setComments,
                        enabled: !state.isBusy,
                      ),
                      const SizedBox(height: 12),
                      SignatureCard(
                        savedStrokes: state.savedStrokes,
                        strokes: state.strokes,
                        onStrokesChanged: cubit.setStrokes,
                        onClear: cubit.clearSignature,
                        rememberSignature: state.rememberSignature,
                        onRememberChanged: cubit.setRememberSignature,
                        enabled: !state.isBusy,
                      ),
                      if (!state.canApprove) ...[
                        const SizedBox(height: 12),
                        Text(t.approveRequirementsHint, style: AppTextStyles.caption),
                      ],
                    ],
                  );
                },
              ),
            ),
            const ReviewFooter(),
          ],
        ),
      ),
    );
  }
}
