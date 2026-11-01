import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/machine_suggestion.dart';
import '../../domain/entities/requests_failure.dart';
import '../bloc/request_suggestions_cubit.dart';
import '../controllers/request_intake_controller.dart';
import 'request_section_parts.dart';
import 'requests_labels.dart';

/// What the loading skeleton is drawn from; only its shape matters.
const _placeholder = MachineSuggestion(
  equipmentId: 'placeholder',
  name: 'Air receiver',
  quantity: 2,
  confidence: 0.9,
  level: SuggestionLevel.high,
  reason: 'name',
  evidence: '1. 2 air receivers',
);

/// "From the email": the client's machines the request's email seems to ask
/// about, each put on the form below with one tap (where it can still be
/// edited), and — while the request has no items — "Fill from email" to fill
/// the form with the clearly named ones at once.
///
/// Needs a [RequestSuggestionsCubit] above it. Shows nothing when there is
/// nothing to suggest and the request already has items.
class RequestSuggestionsPanel extends StatelessWidget {
  /// The form the machines are added to.
  final RequestIntakeController intake;

  const RequestSuggestionsPanel({super.key, required this.intake});

  Future<void> _add(BuildContext context, MachineSuggestion suggestion) async {
    final t = AppLocalizations.of(context)!;
    final added = await intake.addSuggestion(suggestion);
    if (added) MToast.showSuccess(message: t.suggestionAdded);
  }

  Future<void> _autofill(BuildContext context) async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<RequestSuggestionsCubit>();
    final failure = await cubit.autofill();
    if (!context.mounted) return;
    if (failure != null) {
      MToast.showError(message: failure.text(t));
    } else if (cubit.state.request.items.isEmpty) {
      MToast.showError(message: t.fillFromEmailNothing);
    } else {
      MToast.showSuccess(message: t.fillFromEmailDone);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return ListenableBuilder(
      listenable: intake,
      builder: (context, _) => BlocBuilder<RequestSuggestionsCubit, RequestSuggestionsState>(
        builder: (context, state) {
          final loading = state.status == SuggestionsStatus.loading;
          // Filling from the email is for a form with no equipment yet.
          final empty = intake.rows.isEmpty;
          final added = intake.linkedEquipmentIds;
          final open = [
            for (final suggestion in state.suggestions)
              if (!added.contains(suggestion.equipmentId)) suggestion,
          ];
          if (!loading && open.isEmpty && !empty) return const SizedBox.shrink();

          final suggestions = loading ? const [_placeholder] : open;
          return Skeletonizer(
            enabled: loading,
            child: RequestSection(
              color: AppColours.primaryTint,
              borderColor: AppColours.primaryDark.withValues(alpha: 0.1),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Row(
                    children: [
                      const Icon(Icons.auto_awesome_rounded, size: 20, color: AppColours.primaryDark),
                      const SizedBox(width: 10),
                      Expanded(child: SectionTitle(t.fromEmailSuggestionsTitle)),
                      if (suggestions.isNotEmpty)
                        Text(
                          t.itemsCount(suggestions.length),
                          style: rowLabelStyle.copyWith(color: AppColours.inkBody),
                        ),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    t.fromEmailSuggestionsSubtitle,
                    style: AppTextStyles.caption.copyWith(color: AppColours.inkBody),
                  ),
                  if (empty) ...[
                    const SizedBox(height: 14),
                    SizedBox(
                      width: double.infinity,
                      height: 48,
                      child: FilledButton.icon(
                        onPressed: state.isAutofilling || loading ? null : () => _autofill(context),
                        icon: state.isAutofilling
                            ? const SizedBox.square(
                                dimension: 16,
                                child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                              )
                            : const Icon(Icons.mark_email_read_outlined, size: 18),
                        label: Text(t.fillFromEmailAction),
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColours.primaryColor,
                          textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                        ),
                      ),
                    ),
                  ],
                  for (final suggestion in suggestions) ...[
                    const SizedBox(height: 12),
                    _SuggestionTile(
                      suggestion: suggestion,
                      enabled: !state.isAutofilling && !loading,
                      onAdd: () => _add(context, suggestion),
                    ),
                  ],
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

/// One suggested machine: its name and quantity, how sure the match is, the
/// line of the email it came from, and "Add".
class _SuggestionTile extends StatelessWidget {
  final MachineSuggestion suggestion;
  final bool enabled;
  final VoidCallback onAdd;

  const _SuggestionTile({
    required this.suggestion,
    required this.enabled,
    required this.onAdd,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final (tone, levelLabel) = switch (suggestion.level) {
      SuggestionLevel.high => (PillTone.success, t.suggestionLevelHigh),
      SuggestionLevel.medium => (PillTone.info, t.suggestionLevelMedium),
      SuggestionLevel.ambiguous => (PillTone.warning, t.suggestionLevelAmbiguous),
    };
    final reason = switch (suggestion.reason) {
      'serial' => t.suggestionReasonSerial,
      'name' => t.suggestionReasonName,
      'model' => t.suggestionReasonModel,
      _ => null,
    };

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(16)),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const IconTile(icon: Icons.precision_manufacturing_outlined, size: 44),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  suggestion.name,
                  style: AppTextStyles.cardTitle.copyWith(height: 20 / 15),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 2),
                Text(t.machineQuantityLabel(suggestion.quantity), style: AppTextStyles.caption),
                const SizedBox(height: 6),
                StatusPill(label: [levelLabel, ?reason].join(' · '), tone: tone),
                if (suggestion.evidence != null && suggestion.evidence!.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  Text(
                    '“${suggestion.evidence}”',
                    style: AppTextStyles.caption.copyWith(fontStyle: FontStyle.italic),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: 10),
          SizedBox(
            height: 40,
            child: FilledButton(
              onPressed: enabled ? onAdd : null,
              style: FilledButton.styleFrom(
                backgroundColor: AppColours.primaryColor,
                padding: const EdgeInsets.symmetric(horizontal: 16),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
              ),
              child: Text(t.suggestionAddAction, style: AppTextStyles.buttonLabel.copyWith(fontSize: 13)),
            ),
          ),
        ],
      ),
    );
  }
}
