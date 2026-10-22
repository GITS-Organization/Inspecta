import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/presentation/widgets/status_chip.dart';
import '../../domain/entities/timesheet_row.dart';
import 'timesheet_breakdown.dart';
import 'timesheet_details_toggle.dart';
import 'timesheet_labels.dart';
import 'timesheet_returned_note.dart';
import 'timesheet_summary.dart';

/// One accepted job on the Timesheet tab.
///
/// Reads top to bottom as: which job (a quiet line for when and where it
/// stands, then the equipment and client), how it's going with the
/// coordinator (the one coloured pill), the two numbers it comes to, and what
/// you can do about it. The working behind those numbers is a tap away under
/// "Details" rather than spread across the card.
class TimesheetTaskCard extends StatefulWidget {
  final TimesheetRow row;
  final VoidCallback onEdit;

  const TimesheetTaskCard({super.key, required this.row, required this.onEdit});

  @override
  State<TimesheetTaskCard> createState() => _TimesheetTaskCardState();
}

class _TimesheetTaskCardState extends State<TimesheetTaskCard> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final request = widget.row.request;
    final entry = widget.row.entry;

    return Container(
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 8),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Expanded(
                // When, and where the job itself has got to — context, not a
                // status of this screen, so plain text rather than a pill.
                child: Text.rich(
                  TextSpan(
                    children: [
                      TextSpan(
                        text: DateFormat('HH:mm', locale).format(request.scheduledAt!),
                        style: const TextStyle(color: AppColours.ink, fontWeight: FontWeight.w700),
                      ),
                      TextSpan(text: ' · ${request.status.label(t)}'),
                    ],
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w600),
                ),
              ),
              if (entry != null) ...[
                const SizedBox(width: 8),
                TimesheetEntryStatusChip(status: entry.status),
              ],
            ],
          ),
          const SizedBox(height: 8),
          Text(request.equipmentTitle, style: AppTextStyles.cardTitle),
          const SizedBox(height: 2),
          Text(
            '${request.clientName} · ${request.location}',
            style: AppTextStyles.caption.copyWith(height: 18 / 13),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
          const SizedBox(height: 16),
          if (entry == null)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: SizedBox(
                width: double.infinity,
                height: 48,
                child: OutlinedButton.icon(
                  onPressed: widget.onEdit,
                  icon: const Icon(Icons.add_rounded, size: 20),
                  label: Text(t.addTimeAndPriceAction),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColours.ink,
                    side: const BorderSide(color: AppColours.border),
                    textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                  ),
                ),
              ),
            )
          else ...[
            TimesheetSummary(entry: entry),
            if (entry.isReturned) ...[
              const SizedBox(height: 10),
              TimesheetReturnedNote(entry: entry),
            ],
            AnimatedSize(
              duration: const Duration(milliseconds: 200),
              curve: Curves.easeOutCubic,
              alignment: Alignment.topCenter,
              child: _expanded
                  ? Padding(
                      padding: const EdgeInsets.fromLTRB(2, 14, 2, 4),
                      child: TimesheetBreakdown(entry: entry),
                    )
                  : const SizedBox(width: double.infinity),
            ),
            const SizedBox(height: 4),
            // Both ends may shrink, so a longer language or a larger system
            // font wraps a label instead of overflowing the row.
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Flexible(
                  child: TimesheetDetailsToggle(expanded: _expanded, onTap: () => setState(() => _expanded = !_expanded)),
                ),
                const SizedBox(width: 8),
                if (entry.isEditable)
                  Flexible(
                    child: TextButton.icon(
                    onPressed: widget.onEdit,
                    icon: const Icon(Icons.edit_outlined, size: 18),
                    label: Text(t.editAction),
                    style: TextButton.styleFrom(
                      foregroundColor: AppColours.primaryDark,
                      padding: const EdgeInsets.symmetric(horizontal: 12),
                      minimumSize: const Size(0, 44),
                      tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                      textStyle: AppTextStyles.badge.copyWith(fontSize: 13),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    ),
                  )
                else
                  Flexible(
                    child: Tooltip(
                      message: t.timesheetLockedTooltip,
                      child: Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 12),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(Icons.lock_outline_rounded, size: 16, color: AppColours.inkMuted),
                            const SizedBox(width: 6),
                            Flexible(
                              child: Text(
                                t.timesheetLockedLabel,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: AppTextStyles.badge.copyWith(fontSize: 13, color: AppColours.inkMuted),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}
