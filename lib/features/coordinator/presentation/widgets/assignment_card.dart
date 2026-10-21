import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/presentation/widgets/status_chip.dart';
import 'coordinator_labels.dart';

/// One row in the coordinator's queue: a "ready to assign" job shows its
/// due badge and an "Assign inspector" CTA; an already-assigned job shows
/// who it went to and when instead (mockup's mixed list); a job whose
/// certificate the technical manager approved shows a "Send to client" CTA.
class AssignmentCard extends StatelessWidget {
  final InspectionRequest request;
  final String? inspectorName;

  /// Only called when [InspectionRequest.isReadyToAssign] — an already
  /// assigned job isn't tappable yet (mockup's scheduled card is static;
  /// there's no job-detail screen for it here).
  final VoidCallback onAssign;

  /// Only called when [InspectionRequest.isAwaitingSend].
  final VoidCallback onSend;

  const AssignmentCard({
    super.key,
    required this.request,
    required this.onAssign,
    required this.onSend,
    this.inspectorName,
  });

  @override
  Widget build(BuildContext context) {
    final tappable = request.isReadyToAssign || request.isAwaitingSend;
    final content = Padding(
      padding: const EdgeInsets.all(16),
      child: request.isReadyToAssign
          ? _ReadyToAssign(request: request)
          : request.isAwaitingSend
          ? _ReadyToSend(request: request, inspectorName: inspectorName)
          : _Assigned(request: request, inspectorName: inspectorName),
    );

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        boxShadow: [
          BoxShadow(color: AppColours.ink.withValues(alpha: 0.05), blurRadius: 16, offset: const Offset(0, 6)),
          BoxShadow(color: AppColours.ink.withValues(alpha: 0.04), blurRadius: 2),
        ],
      ),
      clipBehavior: Clip.antiAlias,
      child: tappable
          ? Material(
              color: Colors.transparent,
              child: InkWell(onTap: request.isReadyToAssign ? onAssign : onSend, child: content),
            )
          : content,
    );
  }
}

class _ReadyToSend extends StatelessWidget {
  final InspectionRequest request;
  final String? inspectorName;

  const _ReadyToSend({required this.request, this.inspectorName});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final name = inspectorName;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            StatusChip(status: request.status),
            Text(
              request.id,
              style: const TextStyle(fontFamily: 'monospace', fontSize: 12, color: AppColours.inkMuted),
            ),
          ],
        ),
        const SizedBox(height: 10),
        Text(
          request.equipmentTitle,
          style: AppTextStyles.cardTitle.copyWith(fontSize: 16),
          overflow: TextOverflow.ellipsis,
        ),
        const SizedBox(height: 3),
        Text(
          [request.clientName, if (name != null) name].join(' · '),
          style: AppTextStyles.subtitle,
          overflow: TextOverflow.ellipsis,
        ),
        const SizedBox(height: 14),
        SizedBox(
          height: 46,
          width: double.infinity,
          child: FilledButton(
            onPressed: null,
            style: FilledButton.styleFrom(
              backgroundColor: AppColours.primaryColor,
              disabledBackgroundColor: AppColours.primaryColor,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
            ),
            // Purely visual: the whole card already navigates on tap.
            child: IgnorePointer(
              child: Text(t.sendToClientAction, style: AppTextStyles.buttonLabel.copyWith(fontSize: 15)),
            ),
          ),
        ),
      ],
    );
  }
}

class _ReadyToAssign extends StatelessWidget {
  final InspectionRequest request;

  const _ReadyToAssign({required this.request});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final due = request.preferredDate;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            if (due != null)
              _DueBadge(label: due.dueLabel(t))
            else
              const SizedBox.shrink(),
            Text(
              request.id,
              style: const TextStyle(fontFamily: 'monospace', fontSize: 12, color: AppColours.inkMuted),
            ),
          ],
        ),
        const SizedBox(height: 10),
        Text(
          request.equipmentTitle,
          style: AppTextStyles.cardTitle.copyWith(fontSize: 16),
          overflow: TextOverflow.ellipsis,
        ),
        const SizedBox(height: 3),
        Text(
          '${request.clientName} · ${request.location}',
          style: AppTextStyles.subtitle,
          overflow: TextOverflow.ellipsis,
        ),
        const SizedBox(height: 14),
        SizedBox(
          height: 46,
          width: double.infinity,
          child: FilledButton(
            onPressed: null,
            style: FilledButton.styleFrom(
              backgroundColor: AppColours.primaryColor,
              disabledBackgroundColor: AppColours.primaryColor,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
            ),
            // Purely visual: the whole card already navigates on tap.
            child: IgnorePointer(
              child: Text(t.assignInspectorAction, style: AppTextStyles.buttonLabel.copyWith(fontSize: 15)),
            ),
          ),
        ),
      ],
    );
  }
}

class _Assigned extends StatelessWidget {
  final InspectionRequest request;
  final String? inspectorName;

  const _Assigned({required this.request, this.inspectorName});

  static String _initialsOf(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty);
    final letters = parts.take(2).map((p) => p[0].toUpperCase()).join();
    return letters.isEmpty ? '?' : letters;
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final scheduledAt = request.scheduledAt;
    final name = inspectorName;

    return Row(
      children: [
        Container(
          width: 40,
          height: 40,
          alignment: Alignment.center,
          decoration: BoxDecoration(color: AppColours.successBackground, borderRadius: BorderRadius.circular(999)),
          child: Text(
            name == null ? '?' : _initialsOf(name),
            style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: AppColours.successIcon),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(request.equipmentTitle, style: AppTextStyles.cardTitle.copyWith(fontSize: 15)),
              Text(
                [
                  if (name != null) name,
                  if (scheduledAt != null)
                    DateFormat('EEE HH:mm', Localizations.localeOf(context).languageCode).format(scheduledAt),
                ].join(' · '),
                style: AppTextStyles.subtitle.copyWith(fontSize: 13),
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
        const SizedBox(width: 8),
        StatusChip(status: request.status),
      ],
    );
  }
}

class _DueBadge extends StatelessWidget {
  final String label;

  const _DueBadge({required this.label});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
      decoration: BoxDecoration(color: AppColours.chipRedBackground, borderRadius: BorderRadius.circular(999)),
      child: Text(label, style: AppTextStyles.badge.copyWith(color: AppColours.chipRedText)),
    );
  }
}
