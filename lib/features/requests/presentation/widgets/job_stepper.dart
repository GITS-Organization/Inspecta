import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../../core/enums/job_status.dart';

/// The six-step progress bar shared by every job screen (Feature 02 §5):
/// Request → Quote → Assign → Inspect → Certify → Sent. Finished steps are
/// green with a check, the current one is navy, and a dash joins each step to
/// the next (green up to the current step).
class JobStepper extends StatelessWidget {
  final JobStatus status;

  /// The step to show as current (0 = Request … 5 = Sent), for a screen that
  /// is a step of its own whatever the job's status says — the quotation
  /// screen is "Quote" even while the request is still being filled in, and
  /// assigning an accepted quote is "Assign". Defaults to the status's step.
  final int? currentStep;

  const JobStepper({super.key, required this.status, this.currentStep});

  static int _stepIndexOf(JobStatus status) => switch (status) {
    JobStatus.requestReceived => 0,
    JobStatus.quoteDraft ||
    JobStatus.quoteSent ||
    JobStatus.clientCountered ||
    JobStatus.quoteRejected ||
    JobStatus.clientDeclined ||
    JobStatus.quoteAccepted => 1,
    JobStatus.assigned => 2,
    JobStatus.taskAccepted || JobStatus.inProgress => 3,
    JobStatus.certificateSubmitted ||
    JobStatus.certificateReturned ||
    JobStatus.certificateApproved => 4,
    JobStatus.sentToClient => 5,
  };

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final labels = [
      t.stepRequest,
      t.stepQuote,
      t.stepAssign,
      t.stepInspect,
      t.stepCertify,
      t.stepSent,
    ];
    final current = currentStep ?? _stepIndexOf(status);

    return Row(
      children: [
        for (var i = 0; i < labels.length; i++)
          Expanded(
            child: _Step(
              index: i,
              label: labels[i],
              current: current,
              last: i == labels.length - 1,
            ),
          ),
      ],
    );
  }
}

class _Step extends StatelessWidget {
  final int index;
  final String label;
  final int current;
  final bool last;

  const _Step({
    required this.index,
    required this.label,
    required this.current,
    required this.last,
  });

  @override
  Widget build(BuildContext context) {
    final done = index < current;
    final active = index == current;
    final fill = done ? AppColours.successIcon : AppColours.primaryColor;

    // The dash before a step is green when the step before it is finished.
    Widget dash(bool green) => Expanded(
      child: Center(
        child: Container(
          width: 8,
          height: 2,
          decoration: BoxDecoration(
            color: green ? AppColours.successIcon : AppColours.border,
            borderRadius: BorderRadius.circular(1),
          ),
        ),
      ),
    );

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            if (index == 0) const Spacer() else dash(index - 1 < current),
            Container(
              width: 28,
              height: 28,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: done || active ? fill : Colors.white,
                border: done || active ? null : Border.all(color: AppColours.border, width: 2),
                boxShadow: active
                    ? const [BoxShadow(color: AppColours.primaryTint, spreadRadius: 4)]
                    : null,
              ),
              child: done
                  ? const Icon(Icons.check_rounded, size: 14, color: Colors.white)
                  : Text(
                      '${index + 1}',
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w700,
                        color: active ? Colors.white : AppColours.inkMuted,
                      ),
                    ),
            ),
            if (last) const Spacer() else dash(index < current),
          ],
        ),
        const SizedBox(height: 6),
        Text(
          label,
          textAlign: TextAlign.center,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            fontSize: 11,
            fontWeight: active ? FontWeight.w700 : (done ? FontWeight.w600 : FontWeight.w500),
            color: done
                ? AppColours.successText
                : active
                ? AppColours.primaryDark
                : AppColours.inkMuted,
          ),
        ),
      ],
    );
  }
}
