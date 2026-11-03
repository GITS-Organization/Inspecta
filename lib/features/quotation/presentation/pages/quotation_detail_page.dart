import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/utils/currency.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/request_item.dart';
import '../../../requests/domain/entities/request_source.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/domain/usecases/get_request_detail.dart';
import '../../../requests/presentation/pages/request_detail_page.dart';
import '../../../requests/presentation/widgets/job_stepper.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_failure.dart';
import '../../domain/entities/quotation_status.dart';
import '../../domain/entities/reply_type.dart';
import '../../domain/usecases/get_quote_email.dart';
import '../../domain/usecases/get_quote_pdf.dart';
import '../bloc/quotation_detail_cubit.dart';
import '../bloc/quotation_form_cubit.dart';
import '../widgets/client_replies_card.dart';
import '../widgets/quotation_footer.dart';
import '../widgets/quotation_reply_card.dart';
import '../widgets/quote_board_status_chip.dart';
import '../widgets/quote_detail_info_card.dart';
import '../widgets/quote_history_timeline.dart';
import '../widgets/quote_respond_section.dart';
import '../widgets/reject_reason_dialog.dart';
import '../widgets/send_new_price_sheet.dart';
import 'quote_email_page.dart';
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

  /// The request, when the caller already has it; otherwise it is fetched
  /// only if the reply form needs it.
  final InspectionRequest? request;

  const QuotationDetailPage({
    super.key,
    required this.requestId,
    this.quotation,
    this.request,
  });

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<QuotationDetailCubit>(
        param1: requestId,
        param2: (quotation: quotation, request: request),
      ),
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

  /// Runs one of the cubit's actions and says how it went. `true` when it
  /// went through.
  Future<bool> _run(Future<QuotationFailureCode?> Function() action, String success) async {
    final t = AppLocalizations.of(context)!;
    final failure = await action();
    if (!mounted) return false;
    if (failure != null) {
      MToast.showError(message: failure.message(t));
      return false;
    }
    MToast.showSuccess(message: success);
    return true;
  }

  /// Whether the client can still be answered: the request is waiting on them
  /// (or has their reply). Anything else — accepted, rejected, assigned — is
  /// past it, and the API would refuse.
  bool _canRespond(QuotationDetailState state, Quotation current) {
    final status = state.request?.status;
    if (status != null) {
      return status == JobStatus.quoteSent ||
          status == JobStatus.counterOfferSent ||
          status == JobStatus.clientCountered;
    }
    return current.status == QuotationStatus.sent || current.status == QuotationStatus.countered;
  }

  /// Accepting emails the client the PDF: the email is shown first, to read and
  /// edit, and goes out with the accept.
  Future<void> _accept(QuotationDetailCubit cubit, Quotation quote) async {
    final t = AppLocalizations.of(context)!;
    final email = await QuoteEmailPage.open(
      context,
      clientName: quote.clientName,
      accept: true,
      load: () => getIt<GetQuoteEmail>()(requestId: cubit.requestId, quotationId: quote.id, accept: true),
      loadPdf: () => getIt<GetQuotePdf>()(requestId: cubit.requestId, quotationId: quote.id),
    );
    if (email == null || !mounted) return;
    final done = await _run(() => cubit.accept(email: email), t.quotationAcceptedMessage);
    // The request moved on: back to the screen behind, which reloads on `true`.
    if (done && mounted) Navigator.of(context).pop(true);
  }

  Future<void> _reject(QuotationDetailCubit cubit) async {
    final t = AppLocalizations.of(context)!;
    final reason = await showRejectReasonDialog(context);
    if (reason == null || !mounted) return;
    final done = await _run(() => cubit.reject(reason), t.requestRejected);
    if (done && mounted) Navigator.of(context).pop(true);
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
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
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
                              current.status == QuotationStatus.countered || current.hasClientReply
                                  ? _YourTurnChip(label: t.yourTurnBadge)
                                  : QuoteBoardStatusChip(quotation: current),
                            ],
                          ],
                        ),
                        const SizedBox(height: 18),
                        // This screen is the Quote step, whatever the request's
                        // own status says (it may still be "received").
                        JobStepper(
                          status: state.request?.status ?? JobStatus.quoteDraft,
                          currentStep: 1,
                        ),
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
                        // Nothing has gone out yet: this is where the reply is
                        // chosen, priced and sent.
                        if (current == null || current.isDraft) {
                          return _DraftBody(
                            current: current,
                            state: state,
                            onOpenRequest: () => _openRequest(state.request),
                          );
                        }

                        return ListView(
                          padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
                          children: [
                            QuoteDetailInfoCard(
                              current: current,
                              onOpenRequest: () => _openRequest(state.request),
                            ),
                            const SizedBox(height: 12),
                            // The client's emails in answer to this quote.
                            if (current.clientReplies.isNotEmpty) ...[
                              ClientRepliesCard(replies: current.clientReplies),
                              const SizedBox(height: 12),
                            ],
                            QuoteHistoryTimeline(request: state.request, versions: state.versions),
                            const SizedBox(height: 12),
                            if (current.type == ReplyType.offer) ...[
                              SizedBox(
                                width: double.infinity,
                                height: 48,
                                child: OutlinedButton.icon(
                                  // The API generates the proposal PDF.
                                  onPressed: () => PdfPreviewPage.open(
                                    context,
                                    title: t.proposalTitle,
                                    subtitle: current.requestNumber,
                                    fileName: '${current.requestNumber.replaceAll(' ', '-')}.pdf',
                                    allowDownload: false,
                                    build: () => getIt<GetQuotePdf>()(
                                      requestId: cubit.requestId,
                                      quotationId: current.id,
                                    ),
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
                            else if (_canRespond(state, current))
                              QuoteRespondSection(
                                current: current,
                                busy: state.isSubmitting,
                                onReopen: () => _run(cubit.reopen, t.quotationReopened),
                                onSendNewPrice: () => SendNewPriceSheet.show(
                                  context,
                                  nextVersion: (current.version ?? 0) + 1,
                                  currency: current.currency,
                                  onSubmit: cubit.sendNewPrice,
                                ),
                                onAccept: () => _accept(cubit, current),
                                onReject: () => _reject(cubit),
                                onDismissReply: current.hasClientReply
                                    ? () => _run(cubit.dismissReply, t.replyDismissed)
                                    : null,
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

/// What a quote that hasn't been sent shows: its details (from the list row)
/// with the reply options below — Accept, Send price offer or Reject. Nothing
/// is requested to show them: when the request isn't at hand, the form is
/// built from the list row (one price per unit, scope of work, validity,
/// notes), and reads the request's items in the background for when the offer
/// is saved.
/// A successful send closes the screen with `true`, so the screen behind
/// reloads.
class _DraftBody extends StatelessWidget {
  /// The draft, or null when the screen was opened from a request that has
  /// no quote yet.
  final Quotation? current;
  final QuotationDetailState state;
  final VoidCallback onOpenRequest;

  const _DraftBody({
    required this.current,
    required this.state,
    required this.onOpenRequest,
  });

  /// A stand-in for the request, made from the list row: it has the client,
  /// the location and how many units, but not the equipment lines, so it holds
  /// one line for all of them.
  InspectionRequest _provisionalRequest(Quotation quote, String lineTitle) {
    return InspectionRequest(
      id: quote.requestId,
      source: RequestSource.email,
      clientName: quote.clientName,
      receivedAt: quote.createdAt,
      location: quote.location,
      status: JobStatus.quoteDraft,
      items: [
        RequestItem(
          id: '',
          type: lineTitle,
          quantity: quote.totalUnits > 0 ? quote.totalUnits : 1,
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final quote = current;
    final loaded = state.request;

    final details = <Widget>[
      if (quote != null) ...[
        QuoteDetailInfoCard(current: quote, onOpenRequest: onOpenRequest),
        const SizedBox(height: 12),
        QuoteHistoryTimeline(request: loaded, versions: state.versions),
        const SizedBox(height: 12),
      ],
    ];
    const padding = EdgeInsets.fromLTRB(16, 12, 16, 16);

    if (loaded != null && !loaded.isQuotable) {
      return ListView(
        padding: padding,
        children: [
          ...details,
          MNotice(type: MNoticeType.info, message: t.notReadyToQuote),
        ],
      );
    }

    final request =
        loaded ?? (quote == null ? null : _provisionalRequest(quote, t.equipmentLabel));
    if (request == null) return const SizedBox.shrink();

    return BlocProvider(
      key: ValueKey(request.id),
      // A draft keeps the currency it was started in, and a reopened revision
      // comes with the prices, scope, notes and message of the one it replaces.
      create: (_) {
        final form = getIt<QuotationFormCubit>(param1: request, param2: quote?.id)
          ..changeCurrency(quote?.currency ?? Currency.egp);
        if (quote != null) {
          for (final item in quote.items) {
            final price = item.unitPricePiastres;
            if (price <= 0) continue;
            form.changeLinePrice(
              item.requestItemId,
              price % 100 == 0 ? '${price ~/ 100}' : (price / 100).toStringAsFixed(2),
            );
          }
          if (quote.scopeOfWork.isNotEmpty) form.changeScopeOfWork(quote.scopeOfWork);
          if (quote.notes.isNotEmpty) form.changeNotes(quote.notes);
          final message = quote.message;
          if (message != null && message.isNotEmpty) form.changeMessage(message);
        }
        return form;
      },
      child: Column(
        children: [
          Expanded(
            child: ListView(
              padding: padding,
              children: [...details, const QuotationReplyCard()],
            ),
          ),
          QuotationFooter(
            unitsLabel: t.unitsCount(request.totalUnits),
            onSent: () => Navigator.of(context).pop(true),
          ),
        ],
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
