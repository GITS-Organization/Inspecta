import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/widgets/empty_widget.dart';
import '../../../../core/widgets/picker_sheet.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../clients/domain/entities/client.dart';
import '../../../clients/domain/usecases/get_clients.dart';
import '../../../clients/presentation/pages/client_emails_page.dart';
import '../../domain/entities/inspection_request.dart';
import '../../domain/entities/request_source.dart';
import '../../domain/entities/requests_failure.dart';
import '../../domain/entities/requests_tab.dart';
import '../bloc/requests_list_cubit.dart';
import 'request_card.dart';
import 'requests_labels.dart';

/// What the skeleton cards are drawn from; only its shape matters.
final _placeholder = InspectionRequest(
  id: 'placeholder',
  source: RequestSource.email,
  clientName: 'Client name',
  receivedAt: DateTime(2026),
  location: 'Location of the site',
  status: JobStatus.requestReceived,
  items: const [],
  subject: 'Equipment inspection request',
);

/// How far from the end of the list, in pixels, the next page is fetched.
const _loadMoreExtent = 400.0;

/// The section heading and the filtered, paged list of request cards for the
/// tab being shown — with skeleton, error, and empty states.
class RequestsListView extends StatelessWidget {
  final void Function(BuildContext context, InspectionRequest request) onOpen;

  const RequestsListView({super.key, required this.onOpen});

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<RequestsListCubit, RequestsListState>(
      builder: (context, state) {
        final tab = state.current;
        if (tab.status == RequestsListStatus.error) {
          return _ErrorState(failure: tab.failure!);
        }
        if (tab.status == RequestsListStatus.loading) {
          return const _Skeleton();
        }

        final t = AppLocalizations.of(context)!;
        final cubit = context.read<RequestsListCubit>();
        final requests = tab.requests;
        final isMaybe = state.tab == RequestsTab.maybe;

        return RefreshIndicator(
          onRefresh: cubit.refresh,
          child: NotificationListener<ScrollNotification>(
            onNotification: (notification) {
              if (notification.depth == 0 &&
                  notification.metrics.extentAfter < _loadMoreExtent) {
                cubit.loadMore();
              }
              return false;
            },
            child: CustomScrollView(
              physics: const AlwaysScrollableScrollPhysics(),
              slivers: [
                SliverPadding(
                  padding: const EdgeInsets.fromLTRB(20, 20, 20, 8),
                  sliver: SliverToBoxAdapter(
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            _heading(t, state.tab),
                            style: AppTextStyles.sectionTitle,
                          ),
                        ),
                        // The count of what is loaded, with a plus when more
                        // pages remain.
                        _CountPill('${requests.items.length}${requests.hasMore ? '+' : ''}'),
                      ],
                    ),
                  ),
                ),
                if (requests.isEmpty)
                  SliverFillRemaining(
                    hasScrollBody: false,
                    child: _empty(t, state.tab, state.query),
                  )
                else
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 0),
                    sliver: SliverList.separated(
                      itemCount: requests.items.length,
                      separatorBuilder: (_, _) => const SizedBox(height: 12),
                      itemBuilder: (context, index) {
                        final request = requests.items[index];
                        return RequestCard(
                          key: ValueKey(request.id),
                          request: request,
                          onTap: () => onOpen(context, request),
                          onConfirm: isMaybe ? () => _confirm(context, request) : null,
                          onDelete: isMaybe ? () => _delete(context, request) : null,
                          busy: state.busy.contains(request.id),
                        );
                      },
                    ),
                  ),
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(
                      16,
                      12,
                      16,
                      AppLayout.bottomNavClearance,
                    ),
                    child: tab.loadingMore
                        ? Skeletonizer(child: RequestCard(request: _placeholder, onTap: () {}))
                        : null,
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Future<void> _confirm(BuildContext context, InspectionRequest request) async {
    final message = AppLocalizations.of(context)!.confirmRequestError;
    final outcome = await context.read<RequestsListCubit>().confirm(request.id);
    switch (outcome) {
      case ConfirmOutcome.confirmed || ConfirmOutcome.ignored:
        break;
      case ConfirmOutcome.failed:
        MToast.showError(message: message);
      case ConfirmOutcome.clientRequired:
        if (context.mounted) await _registerSender(context, request);
    }
  }

  /// The email's sender isn't registered under a client, so it can't be
  /// confirmed. Offers the way out: pick the client and add the sender's
  /// address (or domain) to it, after which the email becomes its request by
  /// itself — and every other waiting email from that sender with it.
  Future<void> _registerSender(BuildContext context, InspectionRequest request) async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<RequestsListCubit>();
    final sender = request.emailFrom;

    final go = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(t.clientRequiredTitle),
        content: Text(t.clientRequiredMessage(sender ?? request.senderLabel)),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(false), child: Text(t.cancel)),
          TextButton(onPressed: () => Navigator.of(context).pop(true), child: Text(t.addSenderToClientAction)),
        ],
      ),
    );
    if (go != true || !context.mounted) return;

    final client = await PickerSheet.show<Client>(
      context,
      title: t.chooseClientTitle,
      searchHint: t.chooseClientSearchHint,
      emptyText: t.noClientsFound,
      load: getIt<GetClients>().call,
      titleOf: (client) => client.name,
      subtitleOf: (client) => client.location,
    );
    if (client == null || !context.mounted) return;

    final promoted = await Navigator.of(context).push<int>(
      MaterialPageRoute<int>(builder: (_) => ClientEmailsPage(client: client, suggestedSender: sender)),
    );
    // Registering the sender moved waiting emails onto the client: both lists
    // changed.
    if ((promoted ?? 0) > 0) await cubit.refresh();
  }

  Future<void> _delete(BuildContext context, InspectionRequest request) async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<RequestsListCubit>();
    final sure = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(t.deleteRequestTitle),
        content: Text(t.deleteRequestMessage),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(t.cancel),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            style: TextButton.styleFrom(foregroundColor: AppColours.red),
            child: Text(t.deleteRequest),
          ),
        ],
      ),
    );
    if (sure != true) return;
    if (!await cubit.delete(request.id)) MToast.showError(message: t.deleteRequestError);
  }

  Widget _empty(AppLocalizations t, RequestsTab tab, String query) {
    if (query.isNotEmpty) {
      return EmptyState(kind: EmptyKind.search, title: t.emptySearch(query));
    }
    return EmptyState(
      title: switch (tab) {
        RequestsTab.newTab => t.emptyNewRequests,
        RequestsTab.maybe => t.emptyMaybeRequests,
      },
    );
  }

  String _heading(AppLocalizations t, RequestsTab tab) => switch (tab) {
    RequestsTab.newTab => t.needsYourReply,
    RequestsTab.maybe => t.maybeRequestsHeading,
  };
}

/// The round count beside a section heading.
class _CountPill extends StatelessWidget {
  final String label;

  const _CountPill(this.label);

  @override
  Widget build(BuildContext context) {
    return Container(
      constraints: const BoxConstraints(minWidth: 40),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: AppColours.primaryTint,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: AppTextStyles.badge.copyWith(
          fontSize: 14,
          fontWeight: FontWeight.w700,
          color: AppColours.primaryDark,
        ),
      ),
    );
  }
}

/// The list's loading state: cards drawn from [_placeholder], shimmering.
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
        itemBuilder: (_, _) => RequestCard(request: _placeholder, onTap: () {}),
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
