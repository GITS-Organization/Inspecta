import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/presentation/pages/certificate_view_page.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/review_queue_cubit.dart';
import '../widgets/returned_certificate_row.dart';
import '../widgets/review_certificate_card.dart';
import '../widgets/review_header.dart';
import '../widgets/review_stat_tiles.dart';
import 'certificate_review_page.dart';

/// Feature 06's Review tab: certificates waiting for the technical
/// manager's signature, followed by the ones they sent back.
class ReviewQueuePage extends StatelessWidget {
  final VoidCallback onOpenProfile;

  const ReviewQueuePage({super.key, required this.onOpenProfile});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocProvider(
      create: (_) => getIt<ReviewQueueCubit>()..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
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
      ),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body();

  void _review(BuildContext context, InspectionRequest request, String? inspectorName) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => CertificateReviewPage(request: request, inspectorName: inspectorName),
      ),
    );
  }

  void _view(BuildContext context, InspectionRequest request) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => CertificateViewPage(request: request)),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<ReviewQueueCubit, ReviewQueueState>(
      builder: (context, state) {
        if (state.status == ReviewQueueStatus.loading) {
          return Loading.loader(context);
        }
        if (state.status == ReviewQueueStatus.error) {
          return _ErrorState(failure: state.failure!);
        }

        final toReview = state.toReview;
        final returned = state.returned;
        return ListView(
          padding: const EdgeInsets.fromLTRB(16, 20, 16, AppLayout.bottomNavClearance),
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 4),
              child: Text(t.waitingForSignatureTitle, style: AppTextStyles.sectionTitle),
            ),
            const SizedBox(height: 12),
            if (toReview.isEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 24),
                child: Center(child: Text(t.emptyReviewQueue, style: AppTextStyles.subtitle)),
              ),
            for (final request in toReview) ...[
              ReviewCertificateCard(
                key: ValueKey(request.id),
                request: request,
                inspectorName: state.inspectorName(request),
                onOpen: () => _review(context, request, state.inspectorName(request)),
              ),
              const SizedBox(height: 12),
            ],
            for (final request in returned) ...[
              ReturnedCertificateRow(
                key: ValueKey('returned-${request.id}'),
                request: request,
                onOpen: () => _view(context, request),
              ),
              const SizedBox(height: 12),
            ],
          ],
        );
      },
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
