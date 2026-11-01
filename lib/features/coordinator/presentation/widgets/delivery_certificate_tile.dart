import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';

/// A certificate's verdict: satisfactory in green, unsatisfactory in red.
class CertificateResultChip extends StatelessWidget {
  final String result;

  const CertificateResultChip({super.key, required this.result});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final (background, textColor, label) = switch (result.toLowerCase()) {
      'satisfactory' => (AppColours.chipGreenBackground, AppColours.chipGreenText, t.resultSatisfactory),
      'unsatisfactory' => (AppColours.chipRedBackground, AppColours.chipRedText, t.resultUnsatisfactory),
      _ => (AppColours.chipGreyBackground, AppColours.chipGreyText, result),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(999)),
      child: Text(label, style: AppTextStyles.badge.copyWith(color: textColor)),
    );
  }
}

/// A date the API sends as text (`2026-10-31`, or a full timestamp) in the
/// user's language; text that isn't a date is shown as it came.
String formatCertificateDate(BuildContext context, String value) {
  final date = DateTime.tryParse(value);
  if (date == null) return value;
  return DateFormat.yMMMd(Localizations.localeOf(context).languageCode).format(date.toLocal());
}

/// One certificate as a row: its number, the machine it covers, the verdict
/// and the inspection dates, with a "Preview" link to its PDF when
/// [onPreview] is given.
class DeliveryCertificateTile extends StatelessWidget {
  final String number;
  final String? name;
  final String? result;
  final String? examinationDate;
  final String? nextExaminationDate;
  final VoidCallback? onPreview;

  const DeliveryCertificateTile({
    super.key,
    required this.number,
    this.name,
    this.result,
    this.examinationDate,
    this.nextExaminationDate,
    this.onPreview,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final dates = [
      if (examinationDate != null) '${t.examinationDateLabel}: ${formatCertificateDate(context, examinationDate!)}',
      if (nextExaminationDate != null)
        '${t.nextExaminationDateLabel}: ${formatCertificateDate(context, nextExaminationDate!)}',
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                number.isEmpty ? (name ?? '') : t.machineCertificateNumber(number),
                style: AppTextStyles.cardTitle.copyWith(fontSize: 15),
                overflow: TextOverflow.ellipsis,
              ),
            ),
            if (result != null) ...[const SizedBox(width: 8), CertificateResultChip(result: result!)],
          ],
        ),
        if (name != null && number.isNotEmpty) ...[
          const SizedBox(height: 2),
          Text(name!, style: AppTextStyles.caption, overflow: TextOverflow.ellipsis),
        ],
        for (final line in dates) ...[
          const SizedBox(height: 2),
          Text(line, style: AppTextStyles.caption),
        ],
        if (onPreview != null)
          Align(
            alignment: AlignmentDirectional.centerEnd,
            child: InkWell(
              onTap: onPreview,
              borderRadius: BorderRadius.circular(8),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 6),
                child: Text(t.previewAction, style: AppTextStyles.link.copyWith(fontSize: 14)),
              ),
            ),
          ),
      ],
    );
  }
}
