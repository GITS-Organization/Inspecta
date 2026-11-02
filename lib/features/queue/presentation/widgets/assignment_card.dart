import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/presentation/widgets/status_chip.dart';

/// One row in the coordinator's queue: a "ready to assign" job shows its
/// quote number, a short id tag and an "Assign inspector" CTA; an already-assigned job shows
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
    // A delivered job opens the same screen as one to send, read-only.
    final tappable = request.isReadyToAssign || request.isAwaitingSend || request.isDelivered;
    final content = Padding(
      padding: const EdgeInsets.all(20),
      child: request.isReadyToAssign
          ? _ReadyToAssign(request: request)
          : request.isAwaitingSend
          ? _ReadyToSend(request: request, inspectorName: inspectorName)
          : _Assigned(request: request, inspectorName: inspectorName),
    );

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
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

/// The request's id as a small grey tag: the long UUID cut to its first eight
/// characters, so the quote number is the first thing read.
class _IdTag extends StatelessWidget {
  final String id;

  const _IdTag(this.id);

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: AppColours.surfaceMuted,
        borderRadius: BorderRadius.circular(10),
      ),
      child: Text(
        '#${id.length > 8 ? id.substring(0, 8) : id}',
        maxLines: 1,
        style: AppTextStyles.caption.copyWith(
          fontWeight: FontWeight.w600,
          height: 16 / 13,
          color: AppColours.inkSecondary,
        ),
      ),
    );
  }
}

/// The title line of a card: [title] with the [_IdTag] at its end.
class _TitleRow extends StatelessWidget {
  final String title;
  final String id;

  const _TitleRow({required this.title, required this.id});

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Text(
            title,
            style: AppTextStyles.cardTitle.copyWith(fontSize: 17, height: 24 / 17),
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
          ),
        ),
        const SizedBox(width: 10),
        _IdTag(id),
      ],
    );
  }
}

/// The card's filled call-to-action. Purely visual: the whole card already
/// navigates on tap.
class _CardCta extends StatelessWidget {
  final String label;

  const _CardCta(this.label);

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      width: double.infinity,
      child: FilledButton(
        onPressed: null,
        style: FilledButton.styleFrom(
          backgroundColor: AppColours.primaryColor,
          disabledBackgroundColor: AppColours.primaryColor,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
        ),
        child: IgnorePointer(child: Text(label, style: AppTextStyles.buttonLabel)),
      ),
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
        StatusChip(status: request.status),
        const SizedBox(height: 12),
        _TitleRow(title: request.equipmentTitle, id: request.id),
        const SizedBox(height: 2),
        Text(
          [request.clientName, if (name != null) name].join(' · '),
          style: AppTextStyles.caption.copyWith(height: 18 / 13),
        ),
        const SizedBox(height: 16),
        _CardCta(t.sendToClientAction),
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

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        _TitleRow(title: request.equipmentTitle, id: request.id),
        const SizedBox(height: 2),
        // Long client and site names wrap rather than being cut off.
        Text(
          '${request.clientName} · ${request.location}',
          style: AppTextStyles.caption.copyWith(height: 18 / 13),
        ),
        const SizedBox(height: 16),
        _CardCta(t.assignInspectorAction),
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
    final scheduledAt = request.scheduledAt;
    final name = inspectorName;

    return Row(
      children: [
        Container(
          width: 48,
          height: 48,
          alignment: Alignment.center,
          decoration: const BoxDecoration(
            color: AppColours.successBackground,
            shape: BoxShape.circle,
          ),
          child: Text(
            name == null ? '?' : _initialsOf(name),
            style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: AppColours.successText),
          ),
        ),
        const SizedBox(width: 14),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(request.equipmentTitle, style: AppTextStyles.cardTitle, overflow: TextOverflow.ellipsis),
              const SizedBox(height: 2),
              Text(
                [
                  if (name != null) name,
                  if (scheduledAt != null)
                    DateFormat('EEE HH:mm', Localizations.localeOf(context).languageCode).format(scheduledAt),
                ].join(' · '),
                style: AppTextStyles.caption.copyWith(height: 18 / 13),
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
