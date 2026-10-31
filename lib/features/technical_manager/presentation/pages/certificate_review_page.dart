import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../certificate/presentation/widgets/certificate_labels.dart';
import '../../../machines/domain/usecases/get_machine_pdf.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../bloc/review_detail_cubit.dart';
import '../widgets/review_detail_header.dart';
import '../widgets/review_footer.dart';
import '../widgets/review_summary_card.dart';

/// Feature 06's review screen for one machine of a job: the reviewer reads
/// the summary and either approves (which issues the certificate's QR
/// code and hands it to the coordinator to send to the client) or returns the
/// certificate to its inspector.
class CertificateReviewPage extends StatelessWidget {
  final InspectionRequest request;

  /// The machine of the job being reviewed.
  final String requestItemId;
  final String? inspectorName;

  const CertificateReviewPage({
    super.key,
    required this.request,
    required this.requestItemId,
    this.inspectorName,
  });

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) =>
          getIt<ReviewDetailCubit>(param1: request, param2: requestItemId)
            ..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: _ReviewView(
          request: request,
          requestItemId: requestItemId,
          inspectorName: inspectorName,
        ),
      ),
    );
  }
}

class _ReviewView extends StatefulWidget {
  final InspectionRequest request;
  final String requestItemId;
  final String? inspectorName;

  const _ReviewView({
    required this.request,
    required this.requestItemId,
    this.inspectorName,
  });

  @override
  State<_ReviewView> createState() => _ReviewViewState();
}

class _ReviewViewState extends State<_ReviewView> {
  /// The certificate as the API renders it — marked DRAFT until approved.
  Future<void> _viewPdf() {
    final t = AppLocalizations.of(context)!;
    final number = certNumberFor(widget.request);
    return PdfPreviewPage.open(
      context,
      title: t.certificateTitle,
      subtitle: number,
      fileName: '$number.pdf',
      build: () =>
          getIt<GetMachinePdf>()(widget.request.id, widget.requestItemId),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<ReviewDetailCubit>();
    return MultiBlocListener(
      listeners: [
        BlocListener<ReviewDetailCubit, ReviewDetailState>(
          listenWhen: (previous, current) =>
              previous.actionSeq != current.actionSeq,
          listener: (context, state) {
            if (state.lastActionSuccess) {
              MToast.showSuccess(
                message: state.lastActionApproved
                    ? t.certificateApprovedMessage
                    : t.certificateReturnedMessage,
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
                    previous.isBusy != current.isBusy ||
                    previous.canApprove != current.canApprove,
                builder: (context, state) {
                  final certificate = state.certificate;
                  switch (state.status) {
                    case ReviewDetailStatus.loading:
                      return Loading.loader(context);
                    case ReviewDetailStatus.error:
                      return Center(
                        child: Padding(
                          padding: const EdgeInsets.all(24),
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              MNotice(
                                type: MNoticeType.error,
                                message: t.certificateNotFound,
                              ),
                              const SizedBox(height: 16),
                              OutlinedButton(
                                onPressed: cubit.retry,
                                child: Text(t.retry),
                              ),
                            ],
                          ),
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
                        onViewPdf: _viewPdf,
                      ),
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
