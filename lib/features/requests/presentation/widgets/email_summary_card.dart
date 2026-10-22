import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/inspection_request.dart';

/// The intake email's summary, with a link into the full thread (US-02.3).
/// Reading the full thread isn't built yet — see [RequestDetailPage].
class EmailSummaryCard extends StatelessWidget {
  final InspectionRequest request;
  final VoidCallback onReadFullEmail;

  const EmailSummaryCard({
    super.key,
    required this.request,
    required this.onReadFullEmail,
  });

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
    final time = DateFormat.Hm().format(request.receivedAt);

    return MCard(
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
                  _initials(request.clientName),
                  style: const TextStyle(
                    fontSize: 14,
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
                    Text(
                      request.clientName,
                      style: AppTextStyles.cardTitle,
                      overflow: TextOverflow.ellipsis,
                    ),
                    Text(
                      request.emailFrom == null ? time : '${request.emailFrom} · $time',
                      style: AppTextStyles.caption.copyWith(fontSize: 12, height: 16 / 12),
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ),
              ),
            ],
          ),
          if (request.emailPreview != null) ...[
            const SizedBox(height: 12),
            Text(
              request.emailPreview!,
              style: AppTextStyles.body.copyWith(
                fontSize: 14,
                height: 22 / 14,
                color: AppColours.ink,
              ),
            ),
          ],
          const SizedBox(height: 12),
          GestureDetector(
            onTap: onReadFullEmail,
            child: Text(t.readFullEmail, style: AppTextStyles.link),
          ),
        ],
      ),
    );
  }
}
