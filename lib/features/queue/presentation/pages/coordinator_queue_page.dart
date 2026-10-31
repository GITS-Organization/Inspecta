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
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/widgets/user_app_header.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/request_source.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/coordinator_queue_cubit.dart';
import '../widgets/assignment_card.dart';
import '../widgets/coordinator_queue_filters.dart';
import '../../../coordinator/presentation/pages/assign_inspector_page.dart';
import '../../../coordinator/presentation/pages/send_certificate_page.dart';

/// What the skeleton cards are drawn from; only its shape matters.
final _placeholder = InspectionRequest(
  id: 'placeholder',
  source: RequestSource.email,
  clientName: 'Client name',
  receivedAt: DateTime(2026),
  location: 'Location of the site',
  status: JobStatus.quoteAccepted,
  items: const [],
  subject: 'Equipment inspection request',
  preferredDate: DateTime(2099),
);

/// Feature 04 §5's coordinator queue: every job waiting on an inspector,
/// already scheduled/running, or approved and waiting to be sent to the
/// client, newest-due first.
class CoordinatorQueuePage extends StatelessWidget {
  final VoidCallback onOpenProfile;

  const CoordinatorQueuePage({super.key, required this.onOpenProfile});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<CoordinatorQueueCubit>()..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: Scaffold(
          backgroundColor: AppColours.background,
          body: Column(
            children: [
              UserAppHeader(
                title: AppLocalizations.of(context)!.navQueue,
                onOpenProfile: onOpenProfile,
              ),
              const SizedBox(height: 12),
              const CoordinatorQueueFilters(),
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

  // The queue is fetched, not streamed, so it reloads once the user is back
  // from a screen that may have moved the job on.
  Future<void> _open(BuildContext context, InspectionRequest request) async {
    final cubit = context.read<CoordinatorQueueCubit>();
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => AssignInspectorPage(request: request),
      ),
    );
    cubit.start();
  }

  Future<void> _send(BuildContext context, InspectionRequest request) async {
    final cubit = context.read<CoordinatorQueueCubit>();
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => SendCertificatePage(request: request),
      ),
    );
    cubit.start();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<CoordinatorQueueCubit, CoordinatorQueueState>(
      builder: (context, state) {
        if (state.status == CoordinatorQueueStatus.loading) {
          return const _Skeleton();
        }
        if (state.status == CoordinatorQueueStatus.error) {
          return _ErrorState(failure: state.failure!);
        }

        final jobs = state.jobs;
        return RefreshIndicator(
          color: AppColours.primaryColor,
          onRefresh: context.read<CoordinatorQueueCubit>().start,
          child: CustomScrollView(
            // Keeps pull-to-refresh working when the list is short or empty.
            physics: const AlwaysScrollableScrollPhysics(),
            slivers: [
              // Nothing to list: the empty state alone, centred in the space
              // left, without the heading and sort label.
              if (jobs.isEmpty)
                SliverFillRemaining(
                  hasScrollBody: false,
                  child: Padding(
                    padding: const EdgeInsets.only(
                      bottom: AppLayout.bottomNavClearance,
                    ),
                    child: EmptyState(
                      kind: state.filter != null ? EmptyKind.filter : EmptyKind.nodata,
                      title: state.filter != null
                          ? t.emptyCoordinatorQueueFiltered
                          : t.emptyCoordinatorQueue,
                    ),
                  ),
                )
              else ...[
                SliverPadding(
                  padding: const EdgeInsets.fromLTRB(20, 20, 20, 8),
                  sliver: SliverToBoxAdapter(
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      crossAxisAlignment: CrossAxisAlignment.baseline,
                      textBaseline: TextBaseline.alphabetic,
                      children: [
                        Text(
                          t.coordinatorQueueHeading,
                          style: AppTextStyles.sectionTitle,
                        ),
                        Text(t.byDueDate, style: AppTextStyles.caption),
                      ],
                    ),
                  ),
                ),
                SliverPadding(
                  padding: const EdgeInsets.fromLTRB(
                    16,
                    0,
                    16,
                    AppLayout.bottomNavClearance,
                  ),
                  sliver: SliverList.separated(
                    itemCount: jobs.length,
                    separatorBuilder: (_, _) => const SizedBox(height: 12),
                    itemBuilder: (context, index) {
                      final request = jobs[index];
                      return AssignmentCard(
                        key: ValueKey(request.id),
                        request: request,
                        inspectorName: request.inspectorName,
                        onAssign: () => _open(context, request),
                        onSend: () => _send(context, request),
                      );
                    },
                  ),
                ),
              ],
            ],
          ),
        );
      },
    );
  }
}

/// The queue's loading state: cards drawn from [_placeholder], shimmering.
class _Skeleton extends StatelessWidget {
  const _Skeleton();

  @override
  Widget build(BuildContext context) {
    return Skeletonizer(
      child: ListView.separated(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 20, 16, 0),
        itemCount: 4,
        separatorBuilder: (_, _) => const SizedBox(height: 12),
        itemBuilder: (_, _) =>
            AssignmentCard(request: _placeholder, onAssign: () {}, onSend: () {}),
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
            OutlinedButton(
              onPressed: context.read<CoordinatorQueueCubit>().retry,
              child: Text(t.retry),
            ),
          ],
        ),
      ),
    );
  }
}
