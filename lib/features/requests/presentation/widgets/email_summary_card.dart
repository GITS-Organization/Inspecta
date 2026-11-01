import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/inspection_request.dart';
import 'request_section_parts.dart';

/// The intake email: who sent it and when, its subject as a title, and its
/// text — a few lines at first, all of it once "Read full email" is tapped
/// (US-02.3), which then reads "Show less".
class EmailSummaryCard extends StatefulWidget {
  /// Lines of the email shown before it is expanded.
  static const collapsedLines = 3;

  final InspectionRequest request;

  const EmailSummaryCard({super.key, required this.request});

  @override
  State<EmailSummaryCard> createState() => _EmailSummaryCardState();
}

class _EmailSummaryCardState extends State<EmailSummaryCard> {
  bool _expanded = false;

  /// Up to two initials: "Delta Steel Co." → "DS".
  static String _initials(String name) {
    final words = name.trim().split(RegExp(r'\s+')).where((w) => w.isNotEmpty).toList();
    if (words.isEmpty) return '?';
    final letters = words.take(2).map((w) => w.characters.first).join();
    return letters.toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final request = widget.request;
    final time = DateFormat.Hm().format(request.receivedAt);
    final subject = request.subject?.trim() ?? '';
    final body = request.emailPreview?.trim() ?? '';
    final from = request.emailFrom?.trim() ?? '';
    final bodyStyle = AppTextStyles.body.copyWith(
      fontSize: 15,
      height: 23 / 15,
      color: AppColours.inkBody,
    );

    return RequestSection(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Container(
                width: 44,
                height: 44,
                alignment: Alignment.center,
                decoration: const BoxDecoration(
                  color: AppColours.primaryTint,
                  shape: BoxShape.circle,
                ),
                child: Text(
                  _initials(request.senderLabel),
                  style: const TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w700,
                    color: AppColours.primaryDark,
                  ),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.baseline,
                      textBaseline: TextBaseline.alphabetic,
                      children: [
                        Expanded(
                          child: Text(
                            request.senderLabel,
                            style: AppTextStyles.cardTitle.copyWith(fontSize: 16),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        const SizedBox(width: 8),
                        Text(time, style: rowLabelStyle),
                      ],
                    ),
                    if (from.isNotEmpty)
                      Text(
                        from,
                        style: AppTextStyles.caption.copyWith(height: 18 / 13),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                  ],
                ),
              ),
            ],
          ),
          // The email's subject, as the title of what follows.
          if (subject.isNotEmpty) ...[
            const SizedBox(height: 14),
            Text(subject, style: AppTextStyles.cardTitle),
          ],
          if (body.isNotEmpty) ...[
            const SizedBox(height: 6),
            AnimatedSize(
              duration: const Duration(milliseconds: 180),
              alignment: Alignment.topCenter,
              child: SelectableText(
                body,
                style: bodyStyle,
                maxLines: _expanded ? null : EmailSummaryCard.collapsedLines,
              ),
            ),
          ],
          if (body.isNotEmpty || subject.isNotEmpty)
            Align(
              alignment: AlignmentDirectional.centerStart,
              child: UnderlineLink(
                label: _expanded ? t.showLessEmail : t.readFullEmail,
                onPressed: () => setState(() => _expanded = !_expanded),
              ),
            ),
        ],
      ),
    );
  }
}
