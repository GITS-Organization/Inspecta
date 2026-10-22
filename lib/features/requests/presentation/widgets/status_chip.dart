import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../l10n/app_localizations.dart';

extension JobStatusChipStyle on JobStatus {
  String label(AppLocalizations t) => switch (this) {
    JobStatus.requestReceived => t.statusRequestReceived,
    JobStatus.quoteDraft => t.statusQuoteDraft,
    JobStatus.quoteSent => t.statusQuoteSent,
    JobStatus.clientCountered => t.statusClientCountered,
    JobStatus.quoteRejected => t.statusQuoteRejected,
    JobStatus.clientDeclined => t.statusClientDeclined,
    JobStatus.quoteAccepted => t.statusQuoteAccepted,
    JobStatus.assigned => t.statusAssigned,
    JobStatus.taskAccepted => t.statusTaskAccepted,
    JobStatus.inProgress => t.statusInProgress,
    JobStatus.certificateSubmitted => t.statusCertificateSubmitted,
    JobStatus.certificateReturned => t.statusCertificateReturned,
    JobStatus.certificateApproved => t.statusCertificateApproved,
    JobStatus.sentToClient => t.statusSentToClient,
  };

  Color get chipBackground => switch (this) {
    JobStatus.requestReceived || JobStatus.quoteDraft => AppColours.chipAmberBackground,
    JobStatus.quoteSent || JobStatus.clientCountered => AppColours.chipBlueBackground,
    JobStatus.quoteRejected || JobStatus.clientDeclined => AppColours.chipRedBackground,
    JobStatus.certificateReturned => AppColours.chipRedBackground,
    _ => AppColours.chipGreenBackground,
  };

  Color get chipText => switch (this) {
    JobStatus.requestReceived || JobStatus.quoteDraft => AppColours.chipAmberText,
    JobStatus.quoteSent || JobStatus.clientCountered => AppColours.chipBlueText,
    JobStatus.quoteRejected || JobStatus.clientDeclined => AppColours.chipRedText,
    JobStatus.certificateReturned => AppColours.chipRedText,
    _ => AppColours.chipGreenText,
  };
}

/// Small rounded status badge (e.g. "New", "Sent") coloured by [status].
class StatusChip extends StatelessWidget {
  final JobStatus status;

  const StatusChip({super.key, required this.status});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: status.chipBackground,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        status.label(t),
        style: AppTextStyles.badge.copyWith(color: status.chipText),
      ),
    );
  }
}

/// BR-02.8: a request left in New for more than 24 hours.
class WaitingChip extends StatelessWidget {
  const WaitingChip({super.key});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: AppColours.chipRedBackground,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.schedule_rounded, size: 13, color: AppColours.chipRedText),
          const SizedBox(width: 4),
          Text(
            t.waitingOver24h,
            style: AppTextStyles.badge.copyWith(color: AppColours.chipRedText),
          ),
        ],
      ),
    );
  }
}
