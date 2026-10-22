import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_search_field.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/widgets/user_app_header.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_failure.dart';
import '../../domain/entities/quote_board_tab.dart';
import '../../domain/usecases/send_reminder.dart';
import '../bloc/quotations_list_cubit.dart';
import '../widgets/quote_board_card.dart';
import '../widgets/quote_board_tab_bar.dart';
import '../widgets/quote_value_summary.dart';
import '../widgets/quotation_labels.dart';
import 'quotation_detail_page.dart';

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

  void _openDetail(Quotation quotation) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => QuotationDetailPage(requestId: quotation.requestId),
      ),
    );
  }

  Future<void> _sendReminder(Quotation quotation) async {
    final t = AppLocalizations.of(context)!;
    try {
      await getIt<SendReminder>()(quotation.id);
      if (mounted) MToast.showSuccess(message: t.reminderSent);
    } catch (e) {
      if (mounted) {
        MToast.showError(
          message: e is QuotationFailure ? e.code.message(t) : t.actionFailed,
        );
      }
    }
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
                    return Loading.loader(context);
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
                  return ListView(
                    padding: const EdgeInsets.fromLTRB(16, 16, 16, AppLayout.bottomNavClearance),
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
                            onSendReminder: () => _sendReminder(quotation),
                          ),
                          const SizedBox(height: 12),
                        ],
                    ],
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
      QuoteBoardTab.replied => t.boardEmptyReplied,
      QuoteBoardTab.accepted => t.boardEmptyAccepted,
      QuoteBoardTab.lost => t.boardEmptyLost,
    };
  }
}
