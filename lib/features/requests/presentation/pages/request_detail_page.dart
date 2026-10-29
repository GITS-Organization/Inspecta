import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../quotation/presentation/bloc/quotation_form_cubit.dart';
import '../../../quotation/presentation/pages/quotation_detail_page.dart';
import '../../../quotation/presentation/widgets/quotation_footer.dart';
import '../../../quotation/presentation/widgets/quotation_reply_card.dart';
import '../../domain/entities/inspection_request.dart';
import '../../domain/entities/requests_failure.dart';
import '../../domain/usecases/complete_intake.dart';
import '../widgets/complete_request_sheet.dart';
import '../widgets/email_summary_card.dart';
import '../widgets/equipment_grid.dart';
import '../widgets/job_stepper.dart';
import '../widgets/requests_labels.dart';
import '../widgets/status_chip.dart';

/// Feature 02 §5's "Request detail" screen: the six-step progress bar,
/// the intake email, the equipment, and — only while the request is
/// still New/a draft — Feature 03's reply panel. Once a quote has gone
/// out (any [JobStatus] beyond `requestReceived`/`quoteDraft`), the
/// request's story continues in Quotations (see `requests_tab.dart`),
/// so this screen shows a read-only status instead of repeating the
/// same reply picker.
class RequestDetailPage extends StatelessWidget {
  final InspectionRequest request;

  const RequestDetailPage({super.key, required this.request});

  /// Whether this request can receive a reply here — a first quote (it
  /// hasn't been quoted yet, BR-02.5) or a new version after the client's
  /// counter ("Send new price" on the quote detail) — and has enough data
  /// to quote.
  bool get _quotable => request.isQuotable;

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<QuotationFormCubit>(param1: request),
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: SafeArea(
          child: Column(
            children: [
              _Header(request: request),
              Expanded(
                child: _Body(request: request, quotable: _quotable),
              ),
              if (_quotable)
                QuotationFooter(
                  unitsLabel: AppLocalizations.of(context)!
                      .unitsCount(request.totalUnits),
                  // The request has left New/Negotiating for the coordinator
                  // once sent or rejected — back to the inbox.
                  onSent: () => Navigator.of(context).pop(),
                )
              else if (request.isNew)
                _NotReadyNotice(request: request)
              else
                _MovedOnNotice(request: request),
            ],
          ),
        ),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  final InspectionRequest request;
  final bool quotable;

  const _Body({required this.request, required this.quotable});

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
      children: [
        if (request.emailPreview != null || request.emailFrom != null) ...[
          EmailSummaryCard(request: request),
          const SizedBox(height: 12),
        ],
        EquipmentGrid(
          items: request.items,
          preferredDate: request.preferredDate,
          location: request.location,
          clientId: request.clientId,
        ),
        if (request.accessNotes != null && request.accessNotes!.isNotEmpty) ...[
          const SizedBox(height: 12),
          _NotesCard(notes: request.accessNotes!),
        ],
        if (quotable) ...[
          const SizedBox(height: 12),
          const QuotationReplyCard(),
        ],
      ],
    );
  }
}

/// Shown while a request can't be quoted yet, with a button that opens the
/// sheet to fill in what is missing.
class _NotReadyNotice extends StatelessWidget {
  final InspectionRequest request;

  const _NotReadyNotice({required this.request});

  /// Only offer the fix the sheet can make: what it fills in is missing.
  bool get _canComplete =>
      request.clientName.isEmpty || request.location.isEmpty || request.items.isEmpty;

  Future<void> _complete(BuildContext context) async {
    final t = AppLocalizations.of(context)!;
    final completed = await CompleteRequestSheet.show(
      context,
      request: request,
      onSubmit: ({required clientName, required location, accessNotes, required items}) async {
        try {
          await getIt<CompleteIntake>()(
            request: request,
            clientName: clientName,
            location: location,
            accessNotes: accessNotes,
            items: items,
          );
          if (context.mounted) MToast.showSuccess(message: t.requestCompleted);
          return true;
        } catch (e) {
          if (context.mounted) {
            MToast.showError(
              message: e is RequestsFailure
                  ? e.code.message(t)
                  : t.actionFailed,
            );
          }
          return false;
        }
      },
    );
    // The request is a static snapshot on this page — back to the inbox,
    // which reloads on `true`, rather than trying to refresh it in place.
    if (completed == true && context.mounted) Navigator.of(context).pop(true);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: AppColours.border)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          MNotice(type: MNoticeType.info, message: t.notReadyToQuote),
          if (_canComplete) ...[
            const SizedBox(height: 12),
            SizedBox(
              height: 48,
              child: OutlinedButton(
                onPressed: () => _complete(context),
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColours.ink,
                  side: const BorderSide(color: AppColours.border),
                  textStyle: AppTextStyles.buttonLabel.copyWith(
                    color: AppColours.ink,
                  ),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(14),
                  ),
                ),
                child: Text(t.completeRequestAction),
              ),
            ),
          ],
        ],
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

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: AppColours.border)),
      ),
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
            height: 48,
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
                  borderRadius: BorderRadius.circular(14),
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

class _NotesCard extends StatelessWidget {
  final String notes;

  const _NotesCard({required this.notes});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(t.accessNotesLabel, style: AppTextStyles.caption),
          const SizedBox(height: 4),
          Text(
            notes,
            style: AppTextStyles.body.copyWith(
              height: 22 / 14,
              color: AppColours.ink,
            ),
          ),
        ],
      ),
    );
  }
}
