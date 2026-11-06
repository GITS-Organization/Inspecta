import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/presentation/widgets/status_chip.dart';
import 'inspector_design.dart';

/// One job on the Certificates list, drawn as a ticket.
///
/// Above the tear line is the job: where its certificate stands, when it was
/// scheduled, the client, what is being inspected and where. Below it are the
/// two numbers the inspector quotes — the quotation and the certificate — and,
/// for a job still being worked on or sent back, the button into its machines.
/// A job already out of the inspector's hands has no button and does not react
/// to a tap.
class CertificateListCard extends StatelessWidget {
  final InspectionRequest request;

  /// Opens the job's machines; `null` for a job that is already out of the
  /// inspector's hands.
  final VoidCallback? onOpen;

  const CertificateListCard({super.key, required this.request, this.onOpen});

  static const _radius = 26.0;

  /// Still the inspector's to fill in or fix.
  static bool isOpenWork(JobStatus status) =>
      status == JobStatus.inProgress || status == JobStatus.certificateReturned;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final status = request.status;
    final returned = status == JobStatus.certificateReturned;
    final open = isOpenWork(status);

    final scheduled = request.scheduledAt?.toLocal();
    final title = request.senderLabel.isNotEmpty ? request.senderLabel : request.equipmentTitle;
    // The equipment goes under the client, unless the title already is it.
    final equipment = request.items.isNotEmpty && request.senderLabel.isNotEmpty ? request.equipmentTitle : '';
    final units = request.totalUnits;
    final quotation = request.subject?.trim() ?? '';

    // "Working on it" is blue like the Tasks tab; every other stage keeps the
    // job status's own colours.
    final working = status == JobStatus.inProgress;

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(_radius),
        boxShadow: inspectorCardShadow,
        border: returned ? Border.all(color: AppColours.dangerBorder, width: 1.5) : null,
      ),
      child: Material(
        type: MaterialType.transparency,
        child: InkWell(
          borderRadius: BorderRadius.circular(_radius),
          onTap: open ? onOpen : null,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            mainAxisSize: MainAxisSize.min,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(18, 18, 18, 8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Row(
                      children: [
                        Skeleton.leaf(
                          child: StatusPill(
                            label: status.label(t),
                            background: working ? AppColours.chipBlueBackground : status.chipBackground,
                            foreground: working ? AppColours.chipBlueText : status.chipText,
                            dot: working ? const Color(0xFF2F5BFF) : status.chipText,
                            pulsing: working,
                          ),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: scheduled == null
                              ? const SizedBox.shrink()
                              : Align(
                                  alignment: AlignmentDirectional.centerEnd,
                                  child: _Meta(
                                    icon: Icons.calendar_today_outlined,
                                    label: DateFormat('EEE, d MMM · HH:mm', locale).format(scheduled),
                                    muted: true,
                                  ),
                                ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    Text(
                      title,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: inspectorDisplay(context, 20),
                    ),
                    if (equipment.isNotEmpty) ...[
                      const SizedBox(height: 3),
                      Text(
                        equipment,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AppTextStyles.subtitle.copyWith(height: 1.3),
                      ),
                    ],
                    if (request.location.isNotEmpty || units > 0) ...[
                      const SizedBox(height: 10),
                      Wrap(
                        spacing: 18,
                        runSpacing: 8,
                        children: [
                          if (request.location.isNotEmpty)
                            _Meta(icon: Icons.location_on_outlined, label: request.location),
                          if (units > 0) _Meta(icon: Icons.inventory_2_outlined, label: t.unitsCount(units)),
                        ],
                      ),
                    ],
                  ],
                ),
              ),
              const TicketPerforation(),
              Padding(
                padding: const EdgeInsets.fromLTRB(18, 8, 18, 18),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Row(
                      children: [
                        if (quotation.isNotEmpty) ...[
                          Expanded(
                            flex: 3,
                            child: _RefField(
                              label: t.certListQuotation,
                              value: _QuotationValue(quotation),
                            ),
                          ),
                          Container(
                            width: 1,
                            height: 34,
                            margin: const EdgeInsets.symmetric(horizontal: 14),
                            color: AppColours.borderSubtle,
                          ),
                        ],
                        Expanded(
                          flex: quotation.isEmpty ? 1 : 2,
                          child: _RefField(
                            label: t.certListCertificate,
                            value: Text(
                              certNumberFor(request),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: inspectorMono.copyWith(fontSize: 12.5),
                            ),
                          ),
                        ),
                      ],
                    ),
                    if (open) ...[
                      const SizedBox(height: 14),
                      InspectorActionButton(
                        label: returned ? t.machineActionFixResubmit : t.continueCertificateAction,
                        onPressed: onOpen ?? () {},
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// An icon and a short line of the card's meta.
class _Meta extends StatelessWidget {
  final IconData icon;
  final String label;

  /// Fainter, for what is only context (the date).
  final bool muted;

  const _Meta({required this.icon, required this.label, this.muted = false});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: muted ? 14 : 16, color: AppColours.inkMuted),
        SizedBox(width: muted ? 6 : 7),
        Flexible(
          child: Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.subtitle.copyWith(
              fontSize: muted ? 13 : 14,
              fontWeight: FontWeight.w500,
              color: muted ? AppColours.inkMuted : AppColours.inkBody,
            ),
          ),
        ),
      ],
    );
  }
}

/// A small uppercase label over a value: "QUOTATION" / "QT-2026-EDD599 rev02".
class _RefField extends StatelessWidget {
  final String label;
  final Widget value;

  const _RefField({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          isArabic(context) ? label : label.toUpperCase(),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: inspectorEyebrow(context).copyWith(fontSize: 11),
        ),
        const SizedBox(height: 4),
        value,
      ],
    );
  }
}

/// A quotation number with its revision ("rev02") set back in a fainter tone.
class _QuotationValue extends StatelessWidget {
  static final _revision = RegExp(r'^(.*\S)\s+(rev\s*\d+)$', caseSensitive: false);

  final String quotation;

  const _QuotationValue(this.quotation);

  @override
  Widget build(BuildContext context) {
    final style = inspectorMono.copyWith(fontSize: 12.5);
    final match = _revision.firstMatch(quotation);

    return Text.rich(
      match == null
          ? TextSpan(text: quotation)
          : TextSpan(
              text: match.group(1),
              children: [
                TextSpan(
                  text: ' ${match.group(2)}',
                  style: style.copyWith(color: AppColours.inkMuted),
                ),
              ],
            ),
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: style,
    );
  }
}
