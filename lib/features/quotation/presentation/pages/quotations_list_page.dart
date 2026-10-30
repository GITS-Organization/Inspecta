import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_search_field.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/widgets/user_app_header.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_status.dart';
import '../../domain/entities/quote_board_tab.dart';
import '../../domain/entities/reply_type.dart';
import '../bloc/quotations_list_cubit.dart';
import '../widgets/quote_board_card.dart';
import '../widgets/quote_board_tab_bar.dart';
import '../widgets/quote_value_summary.dart';
import '../widgets/quotation_labels.dart';
import 'quotation_detail_page.dart';

/// What the skeleton cards are drawn from; only its shape matters.
final _placeholder = Quotation(
  id: 'placeholder',
  requestId: 'placeholder',
  requestNumber: 'INT/000/2026',
  clientName: 'Client name',
  equipmentSummary: 'Equipment inspection × 2',
  type: ReplyType.offer,
  items: const [],
  status: QuotationStatus.sent,
  createdBy: '',
  createdAt: DateTime(2026),
  version: 1,
  totalPiastres: 100000,
  validUntil: DateTime(2099),
  sentAt: DateTime(2026),
);

/// How far from the end of the board, in pixels, the next page is fetched.
const _loadMoreExtent = 400.0;

/// The Quotations tab: the coordinator's price tracker — every quote
/// they've sent, where it stands, and what needs chasing. A real tab
/// alongside Requests (see [CoordinatorRootPage]), not a pushed screen.
/// Search covers client, request id, and quote number; date-range and
/// client filter dropdowns from the original ask aren't built yet.
class QuotationsListPage extends StatelessWidget {
  final VoidCallback? onOpenProfile;

  const QuotationsListPage({super.key, this.onOpenProfile});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<QuotationsListCubit>()..start(),
      child: _QuotationsBoardView(onOpenProfile: onOpenProfile),
    );
  }
}

class _QuotationsBoardView extends StatefulWidget {
  final VoidCallback? onOpenProfile;

  const _QuotationsBoardView({this.onOpenProfile});

  @override
  State<_QuotationsBoardView> createState() => _QuotationsBoardViewState();
}

class _QuotationsBoardViewState extends State<_QuotationsBoardView> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  /// The board reloads on return: a draft opened here may have been sent.
  Future<void> _openDetail(Quotation quotation) async {
    final cubit = context.read<QuotationsListCubit>();
    final changed = await Navigator.of(context).push<bool>(
      MaterialPageRoute<bool>(
        builder: (_) => QuotationDetailPage(
          requestId: quotation.requestId,
          quotation: quotation,
        ),
      ),
    );
    if (changed == true) cubit.refresh();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<QuotationsListCubit>();

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark,
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: Column(
          children: [
            UserAppHeader(
              title: t.quotationsTitle,
              onOpenProfile: widget.onOpenProfile,
              bottom: ISearchField(
                controller: _searchController,
                onChanged: cubit.setQuery,
                hintText: t.searchQuotesHint,
              ),
            ),
            const SizedBox(height: 12),
            BlocBuilder<QuotationsListCubit, QuotationsListState>(
              buildWhen: (previous, current) =>
                  previous.board != current.board ||
                  previous.tab != current.tab ||
                  previous.onlyExpiringSoon != current.onlyExpiringSoon,
              builder: (context, state) => QuoteBoardTabBar(
                selected: state.tab,
                expiringSoonSelected: state.onlyExpiringSoon,
                expiringSoonCount: state.expiringSoonCount,
                countOf: state.countOf,
                onChanged: cubit.setTab,
                onExpiringSoon: cubit.showExpiringSoon,
              ),
            ),
            Expanded(
              child: BlocBuilder<QuotationsListCubit, QuotationsListState>(
                builder: (context, state) {
                  if (state.status == QuotationsListStatus.loading) {
                    return const _Skeleton();
                  }
                  if (state.status == QuotationsListStatus.error) {
                    return Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            MNotice(
                              type: MNoticeType.error,
                              message: state.failure!.message(t),
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
                  }

                  final visible = state.visible;
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
                      child: ListView(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.fromLTRB(
                          16,
                          16,
                          16,
                          AppLayout.bottomNavClearance,
                        ),
                        children: [
                          Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 4),
                            child: QuoteValueSummary(
                              openTotalPiastres: state.openTotalPiastres,
                            ),
                          ),
                          const SizedBox(height: 12),
                          if (visible.isEmpty)
                            Padding(
                              padding: const EdgeInsets.symmetric(vertical: 32),
                              child: Center(
                                child: Text(
                                  _emptyMessage(t, state),
                                  textAlign: TextAlign.center,
                                  style: AppTextStyles.subtitle,
                                ),
                              ),
                            )
                          else
                            for (final quotation in visible) ...[
                              QuoteBoardCard(
                                key: ValueKey(quotation.id),
                                quotation: quotation,
                                onTap: () => _openDetail(quotation),
                              ),
                              const SizedBox(height: 12),
                            ],
                          if (state.loadingMore)
                            Skeletonizer(
                              child: QuoteBoardCard(quotation: _placeholder, onTap: () {}),
                            ),
                        ],
                      ),
                    ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }

  String _emptyMessage(AppLocalizations t, QuotationsListState state) {
    if (state.query.trim().isNotEmpty)
      return t.boardEmptySearch(state.query.trim());
    if (state.onlyExpiringSoon) return t.boardEmptyOpen;
    return switch (state.tab) {
      QuoteBoardTab.open => t.boardEmptyOpen,
      QuoteBoardTab.accepted => t.boardEmptyAccepted,
      QuoteBoardTab.rejected => t.boardEmptyRejected,
    };
  }
}

/// The board's loading state: cards drawn from [_placeholder], shimmering.
class _Skeleton extends StatelessWidget {
  const _Skeleton();

  @override
  Widget build(BuildContext context) {
    return Skeletonizer(
      child: ListView.separated(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
        itemCount: 3,
        separatorBuilder: (_, _) => const SizedBox(height: 12),
        itemBuilder: (_, _) => QuoteBoardCard(quotation: _placeholder, onTap: () {}),
      ),
    );
  }
}
