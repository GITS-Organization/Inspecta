import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/inspection_request.dart';
import '../../domain/entities/requests_failure.dart';
import '../../domain/entities/requests_tab.dart';
import '../bloc/requests_list_cubit.dart';
import 'request_card.dart';
import 'requests_labels.dart';

/// The tab bar, section heading, and the live, filtered list of request
/// cards — with loading, error, and empty states.
class RequestsListView extends StatelessWidget {
  final void Function(BuildContext context, InspectionRequest request) onOpen;

  const RequestsListView({super.key, required this.onOpen});

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<RequestsListCubit, RequestsListState>(
      builder: (context, state) {
        if (state.status == RequestsListStatus.loading) {
          return Loading.loader(context);
        }
        if (state.status == RequestsListStatus.error) {
          return _ErrorState(failure: state.failure!);
        }

        final t = AppLocalizations.of(context)!;
        final visible = state.visible;

        return CustomScrollView(
          slivers: [
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(20, 20, 20, 8),
              sliver: SliverToBoxAdapter(
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  crossAxisAlignment: CrossAxisAlignment.baseline,
                  textBaseline: TextBaseline.alphabetic,
                  children: [
                    Text(
                      t.tabCount(
                        _heading(t, state.tab),
                        state.countOf(state.tab),
                      ),
                      style: AppTextStyles.sectionTitle.copyWith(
                        fontSize: 14
                      ),
                    ),
                  ],
                ),
              ),
            ),
            if (visible.isEmpty)
              SliverFillRemaining(
                hasScrollBody: false,
                child: _EmptyState(tab: state.tab, query: state.query),
              )
            else
              SliverPadding(
                padding: const EdgeInsets.fromLTRB(
                  16,
                  0,
                  16,
                  AppLayout.bottomNavClearance,
                ),
                sliver: SliverList.separated(
                  itemCount: visible.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, index) {
                    final request = visible[index];
                    return RequestCard(
                      key: ValueKey(request.id),
                      request: request,
                      onTap: () => onOpen(context, request),
                    );
                  },
                ),
              ),
          ],
        );
      },
    );
  }

  String _heading(AppLocalizations t, RequestsTab tab) => switch (tab) {
    RequestsTab.newTab => t.needsYourReply,
    RequestsTab.all => t.allRequestsHeading,
  };
}

class _EmptyState extends StatelessWidget {
  final RequestsTab tab;
  final String query;

  const _EmptyState({required this.tab, required this.query});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final message = query.trim().isNotEmpty
        ? t.emptySearch(query.trim())
        : switch (tab) {
            RequestsTab.newTab => t.emptyNewRequests,
            RequestsTab.all => t.emptyAllRequests,
          };

    return Center(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(
              Icons.inbox_outlined,
              size: 40,
              color: AppColours.inkMuted,
            ),
            const SizedBox(height: 12),
            Text(
              message,
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
              onPressed: context.read<RequestsListCubit>().retry,
              child: Text(t.retry),
            ),
          ],
        ),
      ),
    );
  }
}
