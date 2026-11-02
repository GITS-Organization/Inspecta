import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../quotation/presentation/pages/quotation_detail_page.dart';
import '../../../clients/domain/usecases/get_client_machines.dart';
import '../../domain/entities/inspection_request.dart';
import '../../domain/entities/requests_failure.dart';
import '../../domain/usecases/complete_intake.dart';
import '../../domain/usecases/get_request_detail.dart';
import '../bloc/request_suggestions_cubit.dart';
import '../controllers/request_intake_controller.dart';
import '../widgets/request_details_card.dart';
import '../widgets/request_intake_cards.dart';
import '../widgets/request_section_parts.dart';
import '../widgets/request_suggestions_panel.dart';
import '../widgets/email_summary_card.dart';
import '../widgets/equipment_grid.dart';
import '../widgets/job_stepper.dart';
import '../widgets/requests_labels.dart';
import '../widgets/status_chip.dart';

/// Feature 02 §5's "Request detail" screen: the six-step progress bar,
/// the intake email and the equipment. While the request still lacks what a
/// quote needs (BR-02.5), the screen is a form: the client, the location and
/// the equipment as editable cards, filled from the email in one tap, with
/// "Save & open quotation" at the bottom: one update that also opens the
/// request's quotation (`prepareQuotation`) and goes to the quotation screen.
/// Once the request can be quoted and is still New/a draft, the same button
/// reads "Open quotation". The reply itself ("How do you want to reply?") is
/// chosen only on the quotation screen, never here; a request the client has
/// countered keeps a "Reply to request" button that opens it the same way.
/// Once a quote has gone out (any [JobStatus] beyond
/// `requestReceived`/`quoteDraft`), the request's story continues in
/// Quotations (see `requests_tab.dart`), so this screen shows a read-only
/// status instead.
class RequestDetailPage extends StatefulWidget {
  final InspectionRequest request;

  const RequestDetailPage({super.key, required this.request});

  @override
  State<RequestDetailPage> createState() => _RequestDetailPageState();
}

class _RequestDetailPageState extends State<RequestDetailPage> {
  /// The request as it now is: it changes when it is filled from the email or
  /// its details are saved.
  late InspectionRequest request = widget.request;

  /// What is typed into the client, location and equipment cards.
  late final RequestIntakeController _intake = RequestIntakeController(
    request: widget.request,
    completeIntake: getIt<CompleteIntake>(),
    getRequest: getIt<GetRequestDetail>(),
    getMachines: getIt<GetClientMachines>(),
  );

  /// Whether the details form is open: from the start for a new request that
  /// can't be quoted yet, and again when "Edit details" is tapped.
  late bool _editing = request.isNew && !request.isQuotable;

  /// Whether this request can receive a reply — a first quote (it hasn't been
  /// quoted yet, BR-02.5) or a new version after the client's counter ("Send
  /// new price" on the quote detail) — and has enough data to quote.
  bool get _quotable => request.isQuotable;

  /// A request that is still new opens its quotation from this screen; one the
  /// client has countered keeps the button that opens the quotation details.
  bool get _canOpenQuotation => _quotable && request.isNew && !_editing;

  /// A new request of a known client can be filled from its email: the
  /// client's machines it asks about are suggested.
  bool get _suggest => request.isNew && request.clientId != null;

  @override
  void dispose() {
    _intake.dispose();
    super.dispose();
  }

  /// Saves the details and opens the request's quotation in one update, then
  /// shows the quotation screen. A quotation that is already open, with
  /// nothing edited, is shown as it is: the update would only race the changes
  /// the quotation screen has made since.
  Future<void> _openQuotation() async {
    final t = AppLocalizations.of(context)!;
    try {
      var ready = request;
      if (_editing || request.draftQuoteId == null) {
        final saved = await _intake.save(prepareQuotation: true);
        if (saved == null || !mounted) return;
        final stillEditing = saved.isNew && !saved.isQuotable;
        setState(() {
          request = saved;
          _editing = stillEditing;
        });
        // The API still wants something: the form stays open.
        if (stillEditing) return;
        ready = saved;
      }
      final sent = await Navigator.of(context).push<bool>(
        MaterialPageRoute<bool>(
          builder: (_) => QuotationDetailPage(requestId: ready.id, request: ready),
        ),
      );
      // The reply moved the request on: back to the inbox, which reloads on
      // `true`.
      if (sent == true && mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) MToast.showError(message: e is RequestsFailure ? e.text(t) : t.actionFailed);
    }
  }

  @override
  Widget build(BuildContext context) {
    final content = Column(
      children: [
        _Header(request: request),
        Expanded(
          child: _Body(
            request: request,
            intake: _editing ? _intake : null,
            suggestions: _editing && _suggest ? RequestSuggestionsPanel(intake: _intake) : null,
            onEdit: request.isNew ? () => setState(() => _editing = true) : null,
          ),
        ),
        if (_editing)
          RequestIntakeFooter(intake: _intake, onSave: _openQuotation)
        else if (_canOpenQuotation)
          _OpenQuotationAction(intake: _intake, onOpen: _openQuotation)
        else if (_quotable)
          _ReplyAction(request: request)
        else
          _MovedOnNotice(request: request),
      ],
    );

    return Scaffold(
      backgroundColor: AppColours.background,
      body: SafeArea(
        child: _suggest
            ? BlocProvider(
                create: (_) => getIt<RequestSuggestionsCubit>(param1: widget.request)..start(),
                child: BlocListener<RequestSuggestionsCubit, RequestSuggestionsState>(
                  listenWhen: (previous, current) => previous.request != current.request,
                  listener: (context, state) {
                    // Filled from the email: the form takes what was found.
                    _intake.applyRequest(state.request);
                    setState(() => request = state.request);
                  },
                  child: content,
                ),
              )
            : content,
      ),
    );
  }
}

class _Body extends StatelessWidget {
  final InspectionRequest request;

  /// The details form, while it is open; the request's equipment is shown
  /// read-only otherwise.
  final RequestIntakeController? intake;

  /// The "From the email" panel, while the request can be filled from it.
  final Widget? suggestions;

  /// Opens the details form again, from the read-only equipment.
  final VoidCallback? onEdit;

  const _Body({
    required this.request,
    this.intake,
    this.suggestions,
    this.onEdit,
  });

  @override
  Widget build(BuildContext context) {
    final form = intake;
    return ListView(
      // Dragging the list closes the keyboard, so the form's fields never hide
      // the Save bar's checklist.
      keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 20),
      children: [
        if (request.emailPreview != null || request.emailFrom != null) ...[
          EmailSummaryCard(request: request),
          const SizedBox(height: 14),
        ],
        if (suggestions != null) ...[
          suggestions!,
          const SizedBox(height: 14),
        ],
        if (form != null) ...[
          RequestClientCard(intake: form),
          const SizedBox(height: 14),
          RequestEquipmentCard(intake: form),
        ] else ...[
          RequestDetailsCard(request: request, onEdit: onEdit),
          const SizedBox(height: 14),
          EquipmentGrid(items: request.items),
        ],
      ],
    );
  }
}

/// Shown while a new request has all it needs to be quoted: opens its
/// quotation, where the reply — Accept, Send price offer or Reject — is chosen.
class _OpenQuotationAction extends StatelessWidget {
  final RequestIntakeController intake;
  final Future<void> Function() onOpen;

  const _OpenQuotationAction({required this.intake, required this.onOpen});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return ListenableBuilder(
      listenable: intake,
      builder: (context, _) => RequestFooterBar(
        child: RequestCtaButton(
          label: t.openQuotationAction,
          loading: intake.saving,
          onPressed: onOpen,
        ),
      ),
    );
  }
}

/// Shown while the request can be replied to. The reply itself — Accept,
/// Send price offer or Reject — is chosen on the quotation details screen.
class _ReplyAction extends StatelessWidget {
  final InspectionRequest request;

  const _ReplyAction({required this.request});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return RequestFooterBar(
      child: RequestCtaButton(
        label: t.replyToRequestAction,
        onPressed: () async {
          final sent = await Navigator.of(context).push<bool>(
            MaterialPageRoute<bool>(
              builder: (_) => QuotationDetailPage(
                requestId: request.id,
                request: request,
              ),
            ),
          );
          // The reply moved the request on: back to the inbox, which
          // reloads on `true`.
          if (sent == true && context.mounted) Navigator.of(context).pop(true);
        },
      ),
    );
  }
}

/// Shown once the request has left New/draft: a quote already exists
/// for it, so replying again here would just regress its status —
/// point the coordinator at Quotations instead of repeating the picker.
class _MovedOnNotice extends StatelessWidget {
  final InspectionRequest request;

  const _MovedOnNotice({required this.request});

  static MNoticeType _typeFor(JobStatus status) => switch (status) {
    JobStatus.quoteRejected ||
    JobStatus.clientDeclined ||
    JobStatus.certificateReturned => MNoticeType.error,
    JobStatus.quoteSent || JobStatus.clientCountered => MNoticeType.info,
    _ => MNoticeType.success,
  };

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final status = request.status;
    final reason = request.rejectReason;
    final message =
        status == JobStatus.quoteRejected && reason != null && reason.isNotEmpty
        ? t.requestRejectedReasonMessage(reason)
        : t.requestMovedOnMessage(status.label(t));

    return RequestFooterBar(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          MNotice(
            type: _typeFor(status),
            title: status.label(t),
            message: message,
          ),
          const SizedBox(height: 12),
          SizedBox(
            height: 52,
            child: OutlinedButton.icon(
              onPressed: () => Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (_) => QuotationDetailPage(requestId: request.id),
                ),
              ),
              icon: const Icon(Icons.description_outlined, size: 18),
              label: Text(t.viewInQuotationsAction),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColours.ink,
                side: const BorderSide(color: AppColours.border),
                textStyle: AppTextStyles.buttonLabel.copyWith(
                  color: AppColours.ink,
                ),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(16),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _Header extends StatelessWidget {
  final InspectionRequest request;

  const _Header({required this.request});

  @override
  Widget build(BuildContext context) {
    return AppBanner(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
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
                      request.id,
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
                      request.equipmentTitle,
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
            ],
          ),
          const SizedBox(height: 18),
          JobStepper(status: request.status),
        ],
      ),
    );
  }
}

