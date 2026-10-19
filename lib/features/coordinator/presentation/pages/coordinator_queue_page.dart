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
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/coordinator_queue_cubit.dart';
import '../widgets/assignment_card.dart';
import '../widgets/coordinator_queue_header.dart';
import 'assign_inspector_page.dart';

/// Feature 04 §5's coordinator queue: every job waiting on an inspector
/// or already scheduled/running, newest-due first.
class CoordinatorQueuePage extends StatelessWidget {
  final VoidCallback onOpenProfile;

  const CoordinatorQueuePage({super.key, required this.onOpenProfile});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<CoordinatorQueueCubit>()..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.light,
        child: Scaffold(
          backgroundColor: AppColours.background,
          body: SafeArea(
            bottom: false,
            child: Column(
              children: [
                CoordinatorQueueHeader(onOpenProfile: onOpenProfile),
                const Expanded(child: _Body()),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body();

  void _open(BuildContext context, InspectionRequest request) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => AssignInspectorPage(request: request)),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<CoordinatorQueueCubit, CoordinatorQueueState>(
      builder: (context, state) {
        if (state.status == CoordinatorQueueStatus.loading) {
          return Loading.loader(context);
        }
        if (state.status == CoordinatorQueueStatus.error) {
          return _ErrorState(failure: state.failure!);
        }

        final jobs = state.jobs;
        return CustomScrollView(
          slivers: [
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(20, 18, 20, 8),
              sliver: SliverToBoxAdapter(
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(t.coordinatorQueueHeading, style: AppTextStyles.cardTitle.copyWith(fontSize: 18)),
                    Text(t.byDueDate, style: AppTextStyles.caption),
                  ],
                ),
              ),
            ),
            if (jobs.isEmpty)
              SliverFillRemaining(hasScrollBody: false, child: _EmptyState(filtered: state.filter != null))
            else
              SliverPadding(
                padding: const EdgeInsets.fromLTRB(20, 0, 20, AppLayout.bottomNavClearance),
                sliver: SliverList.separated(
                  itemCount: jobs.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, index) {
                    final request = jobs[index];
                    return AssignmentCard(
                      key: ValueKey(request.id),
                      request: request,
                      inspectorName: state.inspectorName(request.inspectorId),
                      onAssign: () => _open(context, request),
                    );
                  },
                ),
              ),
          ],
        );
      },
    );
  }
}

class _EmptyState extends StatelessWidget {
  /// A header-tile filter is on, so the queue isn't necessarily empty —
  /// just this group of it.
  final bool filtered;

  const _EmptyState({required this.filtered});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Center(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.fact_check_outlined, size: 40, color: AppColours.inkMuted),
            const SizedBox(height: 12),
            Text(
              filtered ? t.emptyCoordinatorQueueFiltered : t.emptyCoordinatorQueue,
              textAlign: TextAlign.center,
              style: AppTextStyles.subtitle,
            ),
          ],
        ),
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
