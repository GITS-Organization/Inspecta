import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/domain/usecases/get_request_detail.dart';
import '../../../requests/presentation/pages/request_detail_page.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../../domain/entities/client_response_outcome.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_status.dart';
import '../../domain/entities/reply_type.dart';
import '../../domain/proposal_terms.dart';
import '../bloc/quotation_detail_cubit.dart';
import '../pdf/proposal_pdf_preview.dart';
import '../widgets/log_client_reply_sheet.dart';
import '../widgets/quote_board_status_chip.dart';
import '../widgets/quote_detail_info_card.dart';
import '../widgets/quote_history_timeline.dart';
import '../widgets/quote_respond_section.dart';
import '../widgets/quotation_labels.dart';

/// The quote detail screen: quote + client identity up top, the full
/// negotiation history as a timeline, and — while there's something to
/// respond to — the actions that move it forward (BR-03.6/03.7).
///
/// An offer can be previewed as its proposal PDF; emailing it to the client
/// needs a backend job this app can't run (BR-03.5), so it isn't sent yet.
class QuotationDetailPage extends StatelessWidget {
  final String requestId;

  /// The quote to show, as the Quotations list has it. Without one the
  /// screen has nothing to display.
  final Quotation? quotation;

  const QuotationDetailPage({super.key, required this.requestId, this.quotation});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) =>
          getIt<QuotationDetailCubit>(param1: requestId, param2: quotation)..start(),
      child: const _QuotationDetailView(),
    );
  }
}

class _QuotationDetailView extends StatefulWidget {
  const _QuotationDetailView();

  @override
  State<_QuotationDetailView> createState() => _QuotationDetailViewState();
}

class _QuotationDetailViewState extends State<_QuotationDetailView> {
  bool _openingRequest = false;

  Future<void> _openRequest(InspectionRequest? loaded) async {
    if (_openingRequest) return;
    setState(() => _openingRequest = true);
    final t = AppLocalizations.of(context)!;
    final requestId = context.read<QuotationDetailCubit>().requestId;
    try {
      final request = loaded ?? await getIt<GetRequestDetail>()(requestId);
      if (!mounted) return;
      if (request == null) {
        MToast.showError(message: t.requestNoLongerAvailable);
        return;
      }
      await Navigator.of(context).push(
        MaterialPageRoute<void>(builder: (_) => RequestDetailPage(request: request)),
      );
    } catch (e) {
      if (mounted) {
        MToast.showError(message: e is RequestsFailure ? e.code.message(t) : t.actionFailed);
      }
    } finally {
      if (mounted) setState(() => _openingRequest = false);
    }
  }

  void _logOtherReply(QuotationDetailCubit cubit) {
    LogClientReplySheet.show(
      context,
      onSubmit: ({required outcome, clientPricePiastres, note}) => cubit.logResponse(
        outcome: outcome,
        clientPricePiastres: clientPricePiastres,
        note: note,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return BlocListener<QuotationDetailCubit, QuotationDetailState>(
      listenWhen: (previous, current) => current.actionSeq != previous.actionSeq,
      listener: (context, state) {
        if (state.lastActionSuccess) {
          MToast.showSuccess(message: t.replyLogged);
        } else {
          MToast.showError(message: state.lastActionFailure!.message(t));
        }
      },
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: SafeArea(
          child: BlocBuilder<QuotationDetailCubit, QuotationDetailState>(
            builder: (context, state) {
              final cubit = context.read<QuotationDetailCubit>();
              final current = state.current;

              return Column(
                children: [
                  AppBanner(
                    child: Row(
                      children: [
                        const MBackButton(),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                current?.id ?? t.quoteDetailTitle,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: AppTextStyles.caption.copyWith(
                                  fontWeight: FontWeight.w600,
                                  letterSpacing: 0.24,
                                  height: 16 / 12,
                                  fontSize: 12,
                                ),
                              ),
                              const SizedBox(height: 2),
                              Text(
                                current?.clientName ?? '',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: AppTextStyles.sectionTitle.copyWith(
                                  fontSize: 22,
                                  height: 28 / 22,
                                  letterSpacing: -0.22,
                                ),
                              ),
                            ],
                          ),
                        ),
                        if (current != null) ...[
                          const SizedBox(width: 8),
                          current.status == QuotationStatus.countered
                              ? _YourTurnChip(label: t.yourTurnBadge)
                              : QuoteBoardStatusChip(quotation: current),
                        ],
                      ],
                    ),
                  ),
                  Expanded(
                    child: Builder(
                      builder: (context) {
                        if (state.status == QuotationDetailStatus.loading) {
                          return Loading.loader(context);
                        }
                        if (state.status == QuotationDetailStatus.error) {
                          return Center(
                            child: Padding(
                              padding: const EdgeInsets.all(24),
                              child: MNotice(
                                type: MNoticeType.error,
                                message: state.failure!.message(t),
                              ),
                            ),
                          );
                        }
                        if (current == null) {
                          return const SizedBox.shrink();
                        }

                        return ListView(
                          padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
                          children: [
                            QuoteDetailInfoCard(
                              current: current,
                              onOpenRequest: () => _openRequest(state.request),
                            ),
                            const SizedBox(height: 12),
                            QuoteHistoryTimeline(request: state.request, versions: state.versions),
                            const SizedBox(height: 12),
                            if (current.type == ReplyType.offer && state.request != null) ...[
                              SizedBox(
                                width: double.infinity,
                                height: 48,
                                child: OutlinedButton.icon(
                                  onPressed: () => previewProposalPdf(
                                    context,
                                    request: state.request!,
                                    lines: current.items,
                                    notes: current.notes.isEmpty ? defaultProposalNotes : current.notes,
                                    scopeOfWork: current.scopeOfWork,
                                    version: current.version ?? 1,
                                    date: current.sentAt ?? current.createdAt,
                                    validUntil: current.validUntil,
                                  ),
                                  icon: const Icon(Icons.picture_as_pdf_outlined, size: 18),
                                  label: Text(t.previewProposal),
                                  style: OutlinedButton.styleFrom(
                                    backgroundColor: Colors.white,
                                    foregroundColor: AppColours.ink,
                                    side: const BorderSide(color: AppColours.border),
                                    textStyle: AppTextStyles.buttonLabel,
                                    shape: RoundedRectangleBorder(
                                      borderRadius: BorderRadius.circular(14),
                                    ),
                                  ),
                                ),
                              ),
                              const SizedBox(height: 12),
                            ],
                            if (current.status == QuotationStatus.accepted)
                              MNotice(
                                type: MNoticeType.success,
                                message: t.readOnlyAcceptedNotice,
                                title: t.acceptedVersionLabel(t.quoteVersionLabel(current.version ?? 0)),
                              )
                            else
                              QuoteRespondSection(
                                current: current,
                                onSendNewPrice: () => _openRequest(state.request),
                                onAcceptClientPrice: current.clientCounterPricePiastres == null
                                    ? null
                                    : () => cubit.logResponse(
                                        outcome: ClientResponseOutcome.accepted,
                                        clientPricePiastres: current.clientCounterPricePiastres,
                                      ),
                                onLogOtherReply: () => _logOtherReply(cubit),
                                onMarkDeclined: () =>
                                    cubit.logResponse(outcome: ClientResponseOutcome.declined),
                              ),
                          ],
                        );
                      },
                    ),
                  ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }
}

class _YourTurnChip extends StatelessWidget {
  final String label;

  const _YourTurnChip({required this.label});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: AppColours.chipBlueBackground,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(label, style: AppTextStyles.badge.copyWith(color: AppColours.chipBlueText)),
    );
  }
}
