import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/widgets/empty_widget.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../machines/presentation/pages/job_machines_page.dart';
import 'review_summary_page.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/request_source.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/review_queue_cubit.dart';
import '../widgets/returned_certificate_row.dart';
import '../widgets/review_certificate_card.dart';
import '../widgets/review_header.dart';
import '../widgets/review_stat_tiles.dart';

/// Feature 06's Review tab: certificates waiting for the technical
/// manager's signature, followed by the ones they sent back. Reads the
/// [ReviewQueueCubit] the technical manager's shell provides, shared with the
/// Sent tab so the queue is fetched once.
class ReviewQueuePage extends StatelessWidget {
  final VoidCallback onOpenProfile;

  const ReviewQueuePage({super.key, required this.onOpenProfile});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark,
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: Column(
          children: [
            ReviewHeader(
              title: t.reviewQueueTitle,
              onOpenProfile: onOpenProfile,
              child: BlocBuilder<ReviewQueueCubit, ReviewQueueState>(
                builder: (context, state) => ReviewStatTiles(
                  toReview: state.toReview.length,
                  returned: state.returned.length,
                  sent: state.sent.length,
                ),
              ),
            ),
            const Expanded(child: _Body()),
          ],
        ),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body();

  // Opens the job's machines, where each one is reviewed. The queue is
  // fetched, not streamed, so it reloads once the user is back from a review
  // that may have approved or returned a certificate.
  //
  // A job waiting on a signature opens its review summary (every machine, with
  // approve-all / reject-all); a returned one still goes through the machines
  // list.
  Future<void> _open(BuildContext context, InspectionRequest request, {bool summary = false}) async {
    final cubit = context.read<ReviewQueueCubit>();
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => summary ? ReviewSummaryPage(request: request) : JobMachinesPage(request: request),
      ),
    );
    cubit.start();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<ReviewQueueCubit, ReviewQueueState>(
      builder: (context, state) {
        if (state.status == ReviewQueueStatus.loading) {
          return const _Skeleton();
        }
        if (state.status == ReviewQueueStatus.error) {
          return _ErrorState(failure: state.failure!);
        }

        final toReview = state.toReview;
        final returned = state.returned;
        if (toReview.isEmpty && returned.isEmpty) {
          return EmptyState(kind: EmptyKind.done, title: t.emptyReviewQueue);
        }
        return ListView(
          padding: const EdgeInsets.fromLTRB(16, 20, 16, AppLayout.bottomNavClearance),
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 4),
              child: Text(t.waitingForSignatureTitle, style: AppTextStyles.sectionTitle),
            ),
            const SizedBox(height: 12),
            if (toReview.isEmpty)
              EmptyState(kind: EmptyKind.done, title: t.emptyReviewQueue, compact: true),
            for (final request in toReview) ...[
              ReviewCertificateCard(
                key: ValueKey(request.id),
                request: request,
                inspectorName: state.inspectorName(request),
                onOpen: () => _open(context, request, summary: true),
              ),
              const SizedBox(height: 12),
            ],
            for (final request in returned) ...[
              ReturnedCertificateRow(
                key: ValueKey('returned-${request.id}'),
                request: request,
                onOpen: () => _open(context, request),
              ),
              const SizedBox(height: 12),
            ],
          ],
        );
      },
    );
  }
}

/// What the skeleton cards are drawn from; only its shape matters.
final _placeholder = InspectionRequest(
  id: 'placeholder',
  source: RequestSource.email,
  clientName: 'Client name',
  receivedAt: DateTime(2026),
  location: 'Location of the site',
  status: JobStatus.certificateSubmitted,
  items: const [],
  subject: 'Quotation number',
  updatedAt: DateTime(2026),
  certificates: const CertificateCounts(total: 2, submitted: 1),
);

/// The queue's loading state: cards drawn from [_placeholder], shimmering.
class _Skeleton extends StatelessWidget {
  const _Skeleton();

  @override
  Widget build(BuildContext context) {
    return Skeletonizer(
      child: ListView.separated(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 20, 16, 0),
        itemCount: 3,
        separatorBuilder: (_, _) => const SizedBox(height: 12),
        itemBuilder: (_, _) =>
            ReviewCertificateCard(request: _placeholder, inspectorName: 'Inspector name', onOpen: () {}),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  final RequestsFailureCode failure;

  const _ErrorState({required this.failure});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            MNotice(type: MNoticeType.error, message: failure.message(t)),
            const SizedBox(height: 16),
            OutlinedButton(onPressed: context.read<ReviewQueueCubit>().retry, child: Text(t.retry)),
          ],
        ),
      ),
    );
  }
}
