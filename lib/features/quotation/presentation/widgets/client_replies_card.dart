import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/utils/relative_time.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/client_reply.dart';

/// What the client wrote back to the quotation, newest on top: who, when, the
/// subject, the text they wrote (without the quoted messages and their
/// signature) and the names of any files they attached.
///
/// Only the text is shown. The email's HTML arrives as the client sent it and
/// can't be trusted, so it is neither read nor rendered.
class ClientRepliesCard extends StatelessWidget {
  final List<ClientReply> replies;

  const ClientRepliesCard({super.key, required this.replies});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final newestFirst = replies.reversed.toList();

    return MCard(
      color: AppColours.primarySoft,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.mark_email_unread_outlined, size: 20, color: AppColours.primaryDark),
              const SizedBox(width: 10),
              Expanded(child: Text(t.clientRepliesTitle, style: AppTextStyles.sectionTitle)),
            ],
          ),
          const SizedBox(height: 4),
          Text(t.clientRepliesHint, style: AppTextStyles.caption),
          for (final reply in newestFirst) ...[
            const SizedBox(height: 12),
            _ReplyTile(reply: reply),
          ],
        ],
      ),
    );
  }
}

class _ReplyTile extends StatelessWidget {
  final ClientReply reply;

  const _ReplyTile({required this.reply});

  static String _size(int bytes) {
    if (bytes <= 0) return '';
    if (bytes < 1024) return '$bytes B';
    if (bytes < 1024 * 1024) return '${(bytes / 1024).round()} KB';
    return '${(bytes / (1024 * 1024)).toStringAsFixed(1)} MB';
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final at = reply.receivedAt;
    final when = at == null ? '' : RelativeDuration.since(at).label(t);
    final text = reply.text.isNotEmpty ? reply.text : reply.subject;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(16)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            when.isEmpty ? reply.sender : t.replyFromLine(reply.sender, when),
            style: AppTextStyles.cardTitle.copyWith(fontSize: 14),
          ),
          if (reply.subject.isNotEmpty && reply.text.isNotEmpty) ...[
            const SizedBox(height: 2),
            Text(reply.subject, style: AppTextStyles.caption, maxLines: 2, overflow: TextOverflow.ellipsis),
          ],
          if (text.isNotEmpty) ...[
            const SizedBox(height: 8),
            SelectableText(
              text,
              style: AppTextStyles.body.copyWith(fontSize: 14.5, height: 1.45, color: AppColours.inkBody),
            ),
          ],
          if (reply.attachments.isNotEmpty) ...[
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 6,
              children: [
                for (final file in reply.attachments)
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                    decoration: BoxDecoration(
                      color: AppColours.surfaceMuted,
                      borderRadius: BorderRadius.circular(999),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(Icons.attach_file_rounded, size: 14, color: AppColours.inkSecondary),
                        const SizedBox(width: 4),
                        Flexible(
                          child: Text(
                            [file.fileName, _size(file.sizeBytes)].where((part) => part.isNotEmpty).join(' · '),
                            style: AppTextStyles.badge.copyWith(color: AppColours.inkBody),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
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
