import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/inspection_request.dart';
import 'request_section_parts.dart';

/// What the request says about the job outside its equipment, read-only: the
/// client, where the inspection takes place and the date the client prefers.
/// "Edit details" opens the form again while the request is still New.
class RequestDetailsCard extends StatelessWidget {
  final InspectionRequest request;

  /// Opens the details form; no "Edit details" action without it.
  final VoidCallback? onEdit;

  const RequestDetailsCard({super.key, required this.request, this.onEdit});

  static bool _has(String? value) => value != null && value.trim().isNotEmpty;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final preferred = request.preferredDate;

    final rows = <Widget>[
      DetailRow(
        icon: Icons.apartment_rounded,
        label: t.clientLabel,
        value: Text(request.senderLabel, maxLines: 2, overflow: TextOverflow.ellipsis, style: AppTextStyles.cardTitle),
        trailing: StatusPill(
          label: request.clientId != null ? t.clientMatchedBadge : t.clientRequiredBadge,
          tone: request.clientId != null ? PillTone.success : PillTone.warning,
        ),
      ),
      if (_has(request.location))
        DetailRow(
          icon: Icons.place_outlined,
          label: t.siteLocationLabel,
          value: Text(request.location, style: AppTextStyles.cardTitle),
        ),
      if (preferred != null)
        DetailRow(
          icon: Icons.event_outlined,
          label: t.preferredDateLabel,
          value: Text(DateFormat.yMMMd(locale).format(preferred), style: AppTextStyles.cardTitle),
        ),
    ];

    return RequestSection(
      padding: EdgeInsets.zero,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsetsDirectional.fromSTEB(18, 18, 10, 8),
            child: Row(
              children: [
                Expanded(child: SectionTitle(t.requestDetailsTitle)),
                if (onEdit != null)
                  UnderlineLink(
                    label: t.editDetailsAction,
                    onPressed: onEdit,
                    fontSize: 13,
                    minHeight: 36,
                  ),
              ],
            ),
          ),
          for (var i = 0; i < rows.length; i++) ...[
            if (i > 0) const RowDivider(),
            rows[i],
          ],
          const SizedBox(height: 6),
        ],
      ),
    );
  }
}
