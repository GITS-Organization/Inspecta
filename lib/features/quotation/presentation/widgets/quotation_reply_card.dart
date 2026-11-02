import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quote_email.dart';
import '../../domain/entities/reply_type.dart';
import '../../domain/proposal_terms.dart';
import '../bloc/quotation_form_cubit.dart';
import '../pages/quote_email_page.dart';
import '../pdf/proposal_pdf_preview.dart';
import 'price_field.dart';
import 'proposal_points_editor.dart';
import 'quote_section_header.dart';
import 'reply_type_switch.dart';

/// Feature 03 §5's "Reply panel": "Your response" — Accept / Send offer /
/// Reject as one switch — then, as cards of their own, the offer's price, the
/// scope of work and notes, and the email to the client (an offer or accept),
/// or the reason (a reject).
class QuotationReplyCard extends StatefulWidget {
  const QuotationReplyCard({super.key});

  @override
  State<QuotationReplyCard> createState() => _QuotationReplyCardState();
}

class _QuotationReplyCardState extends State<QuotationReplyCard> {
  final _priceControllers = <String, TextEditingController>{};
  final _messageController = TextEditingController();

  @override
  void dispose() {
    for (final controller in _priceControllers.values) {
      controller.dispose();
    }
    _messageController.dispose();
    super.dispose();
  }

  /// Opens the email that will carry the PDF and keeps what the coordinator
  /// edited in it. The prices must be filled in first (the email states the
  /// total), so a form that isn't valid yet shows its errors instead.
  Future<void> _openEmail(QuotationFormCubit cubit) async {
    if (!cubit.validate()) return;
    final edits = await QuoteEmailPage.open(
      context,
      clientName: cubit.request.clientName,
      accept: cubit.state.replyType == ReplyType.accept,
      initialEdits: cubit.state.emailEdits,
      load: cubit.loadEmailPreview,
      loadPdf: cubit.fetchPdf,
    );
    if (edits != null) cubit.setEmailEdits(edits);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final cubit = context.watch<QuotationFormCubit>();
    final state = cubit.state;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        MCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(t.yourResponse, style: AppTextStyles.sectionTitle),
              const SizedBox(height: 14),
              ReplyTypeSwitch(
                selected: state.replyType,
                onChanged: cubit.selectReplyType,
              ),
            ],
          ),
        ),
        if (state.isPriced) ...[
          for (final item in cubit.request.items) ...[
            const SizedBox(height: 12),
            MCard(
              child: _LinePricing(
                currency: state.currency,
                onCurrencyChanged: cubit.changeCurrency,
                title: '${item.displayTitle} × ${item.quantity}',
                controller: _priceControllers.putIfAbsent(item.id, TextEditingController.new),
                priceError: state.submitted && state.linePriceInvalid(item.id),
                emphasised: state.replyType == ReplyType.offer,
                onPriceChanged: (value) => cubit.changeLinePrice(item.id, value),
              ),
            ),
          ],
          const SizedBox(height: 12),
          ProposalPointsEditor(
            icon: Icons.checklist_rounded,
            label: t.scopeOfWorkLabel,
            errorText: state.showScopeError ? t.errorScopeRequired : null,
            help: t.proposalNotesHelp,
            addLabel: t.addScopePoint,
            initial: state.scopeOfWork,
            onChanged: cubit.changeScopeOfWork,
            summaryOf: (count, _) => count == 0 ? t.scopeOfWorkEmpty : t.proposalNotesCount(count),
          ),
          const SizedBox(height: 12),
          ProposalPointsEditor(
            icon: Icons.sticky_note_2_outlined,
            label: t.proposalNotesLabel,
            help: t.proposalNotesHelp,
            addLabel: t.addNote,
            initial: state.notes,
            standard: defaultProposalNotes,
            onChanged: cubit.changeNotes,
            summaryOf: (count, isStandard) =>
                isStandard ? t.proposalNotesStandard(count) : t.proposalNotesCount(count),
          ),
        ],
        if (state.isPriced) ...[
          // The offer or accept goes out as an email with the PDF: preview
          // it, and edit it if needed, before sending. It stands in for the
          // message field.
          const SizedBox(height: 12),
          _EmailSection(
            edits: state.emailEdits,
            onTap: () => _openEmail(cubit),
          ),
        ] else if (state.replyType == ReplyType.reject) ...[
          const SizedBox(height: 12),
          MCard(
            child: _MessageField(
              controller: _messageController,
              hasError: state.showReasonError,
              onChanged: cubit.changeMessage,
            ),
          ),
        ],
        if (state.isPriced && !state.priceInvalid) ...[
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            height: 48,
            child: OutlinedButton.icon(
              onPressed: () => cubit.hasQuote
                  // The API generates the PDF from the saved quote, so the
                  // offer is saved first.
                  ? PdfPreviewPage.open(
                      context,
                      title: t.proposalTitle,
                      fileName: 'proposal.pdf',
                      allowDownload: false,
                      build: () async {
                        await cubit.saveOffer();
                        return cubit.fetchPdf();
                      },
                    )
                  : previewProposalPdf(
                      context,
                      request: cubit.request,
                      lines: cubit.offerLines(),
                      scopeOfWork: cubit.scopeOfWork,
                      notes: cubit.notes,
                      validityDays: state.validityDays,
                      currency: state.currency,
                    ),
              icon: const Icon(Icons.picture_as_pdf_outlined, size: 18),
              label: Text(t.previewProposal),
              style: OutlinedButton.styleFrom(
                backgroundColor: Colors.white,
                foregroundColor: AppColours.ink,
                side: const BorderSide(color: AppColours.border),
                textStyle: AppTextStyles.buttonLabel,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
              ),
            ),
          ),
        ],
      ],
    );
  }
}

/// The email to the client, as a card like the scope and notes ones: what
/// goes out (the standard email, or the coordinator's edited version), and a
/// tap opens it to preview and edit.
class _EmailSection extends StatelessWidget {
  final QuoteEmailEdits? edits;
  final VoidCallback onTap;

  const _EmailSection({required this.edits, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final edited = edits != null && !edits!.isEmpty;
    final to = edits?.sentTo;

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
      ),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: QuoteSectionHeader(
            icon: Icons.mail_outline_rounded,
            title: t.emailPreviewTitle,
            summary: !edited
                ? t.quoteEmailStandardSubtitle
                : to == null
                ? t.quoteEmailEditedSubtitle
                : t.quoteEmailEditedToSubtitle(to),
            // It opens the email on a page of its own, not unfolds in place.
            trailing: const Icon(Icons.chevron_right_rounded, color: AppColours.inkMuted),
          ),
        ),
      ),
    );
  }
}

/// The reason a reject tells the client.
class _MessageField extends StatelessWidget {
  final TextEditingController controller;
  final bool hasError;
  final ValueChanged<String> onChanged;

  const _MessageField({
    required this.controller,
    required this.hasError,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(t.reasonToClient, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
        const SizedBox(height: 6),
        TextField(
          controller: controller,
          onChanged: onChanged,
          minLines: 2,
          maxLines: 4,
          style: AppTextStyles.input.copyWith(fontSize: 14, height: 20 / 14, fontWeight: FontWeight.w400),
          decoration: InputDecoration(
            isDense: true,
            contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
            filled: true,
            fillColor: Colors.white,
            border: OutlineInputBorder(
              borderRadius: BorderRadius.circular(14),
              borderSide: const BorderSide(color: AppColours.border),
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(14),
              borderSide: BorderSide(
                color: hasError ? AppColours.errorIcon : AppColours.border,
              ),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(14),
              borderSide: BorderSide(
                color: hasError ? AppColours.errorIcon : AppColours.primaryColor,
                width: 2,
              ),
            ),
          ),
        ),
        if (hasError) ...[
          const SizedBox(height: 6),
          Text(
            t.errorReasonRequired,
            style: AppTextStyles.caption.copyWith(
              color: AppColours.errorIcon,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ],
    );
  }
}

/// One request item's fixed price per visit.
class _LinePricing extends StatelessWidget {
  final String title;
  final String currency;
  final ValueChanged<String> onCurrencyChanged;
  final TextEditingController controller;
  final bool priceError;

  /// Outlines the field in navy: the price is being set, not just confirmed.
  final bool emphasised;
  final ValueChanged<String> onPriceChanged;

  const _LinePricing({
    required this.title,
    required this.currency,
    required this.onCurrencyChanged,
    required this.controller,
    required this.priceError,
    required this.emphasised,
    required this.onPriceChanged,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(title, style: AppTextStyles.sectionTitle),
        const SizedBox(height: 2),
        Text(t.fixedPricePerVisit, style: AppTextStyles.caption),
        const SizedBox(height: 12),
        PriceField(
          currency: currency,
          hintText: '0.00',
          emphasised: emphasised,
          // One currency for the whole quote: picking it on any line changes
          // every line.
          onCurrencyChanged: onCurrencyChanged,
          controller: controller,
          onChanged: onPriceChanged,
          errorText: priceError ? t.errorPriceRequired : null,
        ),
      ],
    );
  }
}
