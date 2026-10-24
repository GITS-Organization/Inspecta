import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/reply_type.dart';
import '../../domain/proposal_terms.dart';
import '../bloc/quotation_form_cubit.dart';
import '../pdf/proposal_pdf_preview.dart';
import 'price_field.dart';
import 'proposal_points_editor.dart';

import 'reply_option_tile.dart';

/// Feature 03 §5's "Reply panel (on Request detail)": Accept / Offer
/// price / Reject, with the offer's price, validity, and message fields.
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

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final cubit = context.watch<QuotationFormCubit>();
    final state = cubit.state;

    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(t.howDoYouWantToReply, style: AppTextStyles.sectionTitle),
          const SizedBox(height: 12),
          ReplyOptionTile(
            title: t.replyAccept,
            subtitle: t.replyAcceptSubtitle,
            selected: state.replyType == ReplyType.accept,
            onTap: () => cubit.selectReplyType(ReplyType.accept),
          ),
          const SizedBox(height: 12),
          ReplyOptionTile(
            title: t.replyOffer,
            subtitle: t.replyOfferSubtitle,
            selected: state.replyType == ReplyType.offer,
            onTap: () => cubit.selectReplyType(ReplyType.offer),
          ),
          const SizedBox(height: 12),
          ReplyOptionTile(
            title: t.replyReject,
            subtitle: t.replyRejectSubtitle,
            danger: true,
            selected: state.replyType == ReplyType.reject,
            onTap: () => cubit.selectReplyType(ReplyType.reject),
          ),
          if (state.replyType == ReplyType.offer) ...[
            for (final item in cubit.request.items) ...[
              const SizedBox(height: 14),
              _LinePricing(
                title: '${item.displayTitle} × ${item.quantity}',
                controller: _priceControllers.putIfAbsent(item.id, TextEditingController.new),
                priceError: state.submitted && state.linePriceInvalid(item.id),
                onPriceChanged: (value) => cubit.changeLinePrice(item.id, value),
              ),
            ],
            const SizedBox(height: 14),
            Text(t.validFor, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
            const SizedBox(height: 6),
            _ValidityDropdown(value: state.validityDays, onChanged: cubit.changeValidity),
            const SizedBox(height: 14),
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
            const SizedBox(height: 14),
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
          if (state.replyType == ReplyType.offer || state.replyType == ReplyType.reject) ...[
            const SizedBox(height: 14),
            _MessageField(
              controller: _messageController,
              required: state.replyType == ReplyType.reject,
              hasError: state.showReasonError,
              onChanged: cubit.changeMessage,
            ),
          ],
          if (state.replyType == ReplyType.offer && !state.priceInvalid) ...[
            const SizedBox(height: 14),
            SizedBox(
              width: double.infinity,
              height: 48,
              child: OutlinedButton.icon(
                onPressed: () => previewProposalPdf(
                  context,
                  request: cubit.request,
                  lines: cubit.offerLines(),
                  scopeOfWork: cubit.scopeOfWork,
                  notes: cubit.notes,
                  validityDays: state.validityDays,
                ),
                icon: const Icon(Icons.picture_as_pdf_outlined, size: 18),
                label: Text(t.previewProposal),
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColours.ink,
                  side: const BorderSide(color: AppColours.border),
                  textStyle: AppTextStyles.buttonLabel,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _ValidityDropdown extends StatelessWidget {
  final int value;
  final ValueChanged<int> onChanged;

  const _ValidityDropdown({required this.value, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return _DropdownBox<int>(
      value: value,
      items: const [14, 30],
      labelOf: t.validityDaysOption,
      onChanged: onChanged,
    );
  }
}

class _DropdownBox<T> extends StatelessWidget {
  final T value;
  final List<T> items;
  final String Function(T) labelOf;
  final ValueChanged<T> onChanged;

  const _DropdownBox({
    required this.value,
    required this.items,
    required this.labelOf,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 48,
      padding: const EdgeInsets.symmetric(horizontal: 14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColours.border),
      ),
      child: DropdownButtonHideUnderline(
        child: DropdownButton<T>(
          value: value,
          isExpanded: true,
          style: AppTextStyles.input.copyWith(fontSize: 15),
          items: items
              .map((item) => DropdownMenuItem(value: item, child: Text(labelOf(item), overflow: TextOverflow.ellipsis)))
              .toList(),
          onChanged: (item) {
            if (item != null) onChanged(item);
          },
        ),
      ),
    );
  }
}

class _MessageField extends StatelessWidget {
  final TextEditingController controller;
  final bool required;
  final bool hasError;
  final ValueChanged<String> onChanged;

  const _MessageField({
    required this.controller,
    required this.required,
    required this.hasError,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final label = required ? t.reasonToClient : '${t.messageToClient} (${t.optionalLabel})';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
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
  final TextEditingController controller;
  final bool priceError;
  final ValueChanged<String> onPriceChanged;

  const _LinePricing({
    required this.title,
    required this.controller,
    required this.priceError,
    required this.onPriceChanged,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(title, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
        const SizedBox(height: 2),
        Text(t.fixedPricePerVisit, style: AppTextStyles.caption),
        const SizedBox(height: 6),
        PriceField(
          controller: controller,
          onChanged: onPriceChanged,
          errorText: priceError ? t.errorPriceRequired : null,
        ),
      ],
    );
  }
}
