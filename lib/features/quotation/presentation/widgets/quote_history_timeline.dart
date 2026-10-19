import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/utils/currency.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/request_source.dart';
import '../../domain/entities/client_response_outcome.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_status.dart';
import '../../domain/entities/reply_type.dart';

/// The quote detail's negotiation history: a vertical timeline from the
/// request landing to whatever's next — every version sent and the
/// client's answer to each, ending in a "your response" prompt when it's
/// the coordinator's turn to act.
class QuoteHistoryTimeline extends StatelessWidget {
  final InspectionRequest? request;

  /// Newest first, as [QuotationDetailState.versions] provides it.
  final List<Quotation> versions;

  const QuoteHistoryTimeline({super.key, required this.request, required this.versions});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final dateFormat = DateFormat.yMMMd(Localizations.localeOf(context).languageCode);
    final chronological = versions.reversed.toList();
    final latest = versions.isEmpty ? null : versions.first;
    final awaitingCoordinator = latest?.status == QuotationStatus.countered;

    final steps = <_Step>[
      if (request != null) _requestReceivedStep(t, dateFormat, request!),
      for (final version in chronological) ...[
        _sentStep(t, dateFormat, version),
        if (version.clientResponseOutcome != null) _responseStep(t, dateFormat, version),
      ],
      if (awaitingCoordinator) _yourResponseStep(t),
    ];

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        boxShadow: [
          BoxShadow(
            color: AppColours.ink.withValues(alpha: 0.05),
            blurRadius: 16,
            offset: const Offset(0, 6),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(t.negotiationHistoryTitle, style: AppTextStyles.cardTitle.copyWith(fontSize: 16)),
          const SizedBox(height: 8),
          for (var i = 0; i < steps.length; i++)
            _TimelineRow(step: steps[i], showLine: i != steps.length - 1),
        ],
      ),
    );
  }

  _Step _requestReceivedStep(
    AppLocalizations t,
    DateFormat dateFormat,
    InspectionRequest request,
  ) {
    final source = request.source == RequestSource.email ? t.timelineViaEmail : t.timelineViaManual;
    return _Step(
      icon: const Icon(Icons.inbox_rounded, size: 16),
      iconBackground: AppColours.chipBlueBackground,
      iconColor: AppColours.primaryDark,
      title: Text(t.timelineRequestReceived, style: AppTextStyles.cardTitle.copyWith(fontSize: 14)),
      meta: t.timelineReceivedVia(dateFormat.format(request.receivedAt), source),
    );
  }

  _Step _sentStep(AppLocalizations t, DateFormat dateFormat, Quotation version) {
    if (version.type == ReplyType.reject) {
      return _Step(
        icon: const Icon(Icons.close_rounded, size: 16),
        iconBackground: AppColours.chipGreyBackground,
        iconColor: AppColours.chipGreyText,
        title: Text(t.timelineRejectedByYou, style: AppTextStyles.cardTitle.copyWith(fontSize: 14)),
        meta: t.timelineSentByYouMeta(dateFormat.format(version.sentAt ?? version.createdAt)),
      );
    }
    if (version.status == QuotationStatus.draft) {
      return _Step(
        icon: const Icon(Icons.edit_outlined, size: 16),
        iconBackground: AppColours.surfaceMuted,
        iconColor: AppColours.inkMuted,
        title: Text(t.timelineDraft, style: AppTextStyles.cardTitle.copyWith(fontSize: 14)),
        meta: t.timelineSentByYouMeta(dateFormat.format(version.createdAt)),
      );
    }

    var meta = t.timelineSentByYouMeta(dateFormat.format(version.sentAt ?? version.createdAt));
    final validUntil = version.validUntil;
    if (validUntil != null) {
      final days = validUntil.difference(version.sentAt ?? version.createdAt).inDays;
      if (days > 0) meta = '$meta · ${t.timelineValidDays(days)}';
    }

    return _Step(
      iconBackground: AppColours.primaryColor,
      iconColor: Colors.white,
      badgeLabel: t.quoteVersionLabel(version.version ?? 0),
      title: Row(
        children: [
          Expanded(
            child: Text(
              t.timelineSent(version.version ?? 0),
              style: AppTextStyles.cardTitle.copyWith(fontSize: 14),
              overflow: TextOverflow.ellipsis,
            ),
          ),
          if (version.totalPiastres != null)
            Text(
              Currency.formatEgp(version.totalPiastres!),
              style: AppTextStyles.cardTitle.copyWith(fontSize: 15),
            ),
        ],
      ),
      meta: meta,
    );
  }

  _Step _responseStep(AppLocalizations t, DateFormat dateFormat, Quotation version) {
    final outcome = version.clientResponseOutcome!;
    final (icon, background, color) = switch (outcome) {
      ClientResponseOutcome.counter => (
        Icons.reply_rounded,
        AppColours.chipAmberBackground,
        AppColours.chipAmberText,
      ),
      ClientResponseOutcome.accepted => (
        Icons.check_rounded,
        AppColours.successBackground,
        AppColours.successIcon,
      ),
      ClientResponseOutcome.declined => (
        Icons.close_rounded,
        AppColours.errorBackground,
        AppColours.errorIcon,
      ),
    };
    final title = switch (outcome) {
      ClientResponseOutcome.counter => t.timelineClientCountered(
        version.clientCounterPricePiastres == null
            ? ''
            : Currency.formatEgp(version.clientCounterPricePiastres!),
      ),
      ClientResponseOutcome.accepted => t.timelineClientAccepted,
      ClientResponseOutcome.declined => t.timelineClientDeclined,
    };
    final note = version.clientResponseNote?.trim();

    return _Step(
      icon: Icon(icon, size: 16),
      iconBackground: background,
      iconColor: color,
      title: Text(title, style: AppTextStyles.cardTitle.copyWith(fontSize: 14)),
      meta: version.clientRespondedAt == null ? null : dateFormat.format(version.clientRespondedAt!),
      note: note == null || note.isEmpty ? null : note,
      noteBackground: background.withValues(alpha: 0.5),
      noteColor: color,
    );
  }

  _Step _yourResponseStep(AppLocalizations t) {
    return _Step(
      dashed: true,
      title: Text(
        t.timelineYourResponseTitle,
        style: AppTextStyles.cardTitle.copyWith(fontSize: 14, color: AppColours.primaryDark),
      ),
      meta: t.timelineChooseOption,
    );
  }
}

/// One negotiation-history entry's content, independent of its position
/// in the list (the icon column and connecting line are drawn by
/// [_TimelineRow] around it).
class _Step {
  final Widget? icon;
  final Color? iconBackground;
  final Color? iconColor;
  final String? badgeLabel;
  final bool dashed;
  final Widget title;
  final String? meta;
  final String? note;
  final Color? noteBackground;
  final Color? noteColor;

  const _Step({
    this.icon,
    this.iconBackground,
    this.iconColor,
    this.badgeLabel,
    this.dashed = false,
    required this.title,
    this.meta,
    this.note,
    this.noteBackground,
    this.noteColor,
  });
}

class _TimelineRow extends StatelessWidget {
  final _Step step;
  final bool showLine;

  const _TimelineRow({required this.step, required this.showLine});

  @override
  Widget build(BuildContext context) {
    // IntrinsicHeight gives the connecting line's Expanded a bounded
    // height to stretch into (a bare CrossAxisAlignment.stretch here
    // would demand infinite height and crash layout — see the board
    // card's counter-offer price row for the same fix).
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Column(
            children: [
              _StepIcon(step: step),
              if (showLine)
                Expanded(
                  child: Container(
                    width: 2,
                    margin: const EdgeInsets.symmetric(vertical: 4),
                    color: AppColours.border,
                  ),
                ),
            ],
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(bottom: showLine ? 18 : 4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  step.title,
                  if (step.meta != null) ...[
                    const SizedBox(height: 3),
                    Text(step.meta!, style: AppTextStyles.caption),
                  ],
                  if (step.note != null) ...[
                    const SizedBox(height: 8),
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                      decoration: BoxDecoration(
                        color: step.noteBackground ?? AppColours.surfaceMuted,
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Text(
                        '"${step.note}"',
                        style: AppTextStyles.subtitle.copyWith(
                          fontSize: 13,
                          color: step.noteColor ?? AppColours.inkBody,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _StepIcon extends StatelessWidget {
  final _Step step;

  const _StepIcon({required this.step});

  @override
  Widget build(BuildContext context) {
    if (step.dashed) {
      return Container(
        width: 32,
        height: 32,
        decoration: BoxDecoration(
          color: Colors.white,
          shape: BoxShape.circle,
          // A one-off dashed-ring placeholder colour — not a reusable
          // design token, so it's inlined rather than added to AppColours.
          border: Border.all(color: const Color(0xFF93B4F8), width: 2),
        ),
      );
    }
    return Container(
      width: 32,
      height: 32,
      decoration: BoxDecoration(color: step.iconBackground, shape: BoxShape.circle),
      alignment: Alignment.center,
      child: step.badgeLabel != null
          ? Text(
              step.badgeLabel!,
              style: AppTextStyles.badge.copyWith(fontSize: 12, color: step.iconColor, height: 1),
            )
          : IconTheme(
              data: IconThemeData(color: step.iconColor),
              child: step.icon ?? const SizedBox.shrink(),
            ),
    );
  }
}
