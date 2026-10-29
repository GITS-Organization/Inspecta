import 'package:flutter/material.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/utils/relative_time.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/inspection_request.dart';
import 'status_chip.dart';

class RequestCard extends StatelessWidget {
  final InspectionRequest request;
  final VoidCallback onTap;
  final VoidCallback? onConfirm;
  final VoidCallback? onDelete;
  final bool busy;

  const RequestCard({
    super.key,
    required this.request,
    required this.onTap,
    this.onConfirm,
    this.onDelete,
    this.busy = false,
  });

  bool get _isMaybe => onConfirm != null && onDelete != null;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final waiting = request.isWaitingOver24h();

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
      ),
      clipBehavior: Clip.antiAlias,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const _IconTile(Icons.precision_manufacturing_outlined),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            request.equipmentTitle,
                            style: AppTextStyles.cardTitle,
                            overflow: TextOverflow.ellipsis,
                          ),
                          const SizedBox(height: 2),
                          Row(
                            children: [
                              Flexible(
                                child: Text(
                                  // An email has no equipment yet, so no unit count.
                                  request.totalUnits == 0
                                      ? request.senderLabel
                                      : '${request.senderLabel} · ${t.unitsCount(request.totalUnits)}',
                                  style: AppTextStyles.caption.copyWith(
                                    height: 18 / 13,
                                  ),
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                              if (request.isNewClient) ...[
                                const SizedBox(width: 6),
                                _NewClientTag(label: t.newClientTag),
                              ],
                            ],
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 8),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        StatusChip(status: request.status),
                        if (waiting) ...[
                          const SizedBox(height: 6),
                          const WaitingChip(),
                        ],
                      ],
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                Row(
                  children: [
                    if (request.location.isNotEmpty)
                      _MetaItem(
                        icon: Icons.location_on_outlined,
                        label: request.location,
                      )
                    else if (request.emailFrom != null)
                      _MetaItem(
                        icon: Icons.mail_outline_rounded,
                        label: request.emailFrom!,
                      ),
                    const SizedBox(width: 20),
                    _MetaItem(
                      icon: Icons.schedule_outlined,
                      label: RelativeDuration.since(request.receivedAt)
                          .label(t),
                    ),
                  ],
                ),
                if (_isMaybe) ...[
                  const SizedBox(height: 16),
                  _MaybeActions(
                    onConfirm: onConfirm!,
                    onDelete: onDelete!,
                    busy: busy,
                  ),
                ] else if (request.isNew) ...[
                  const SizedBox(height: 16),
                  _ReviewCta(
                    primary: request.status == JobStatus.requestReceived,
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _IconTile extends StatelessWidget {
  final IconData icon;

  const _IconTile(this.icon);

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 48,
      height: 48,
      decoration: BoxDecoration(
        color: AppColours.primaryTint,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Icon(icon, size: 22, color: AppColours.primaryColor),
    );
  }
}

class _NewClientTag extends StatelessWidget {
  final String label;

  const _NewClientTag({required this.label});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
      decoration: BoxDecoration(
        color: AppColours.chipGreyBackground,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: AppTextStyles.badge.copyWith(
          fontSize: 10.5,
          color: AppColours.chipGreyText,
        ),
      ),
    );
  }
}

class _MetaItem extends StatelessWidget {
  final IconData icon;
  final String label;

  const _MetaItem({required this.icon, required this.label});

  @override
  Widget build(BuildContext context) {
    return Flexible(
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 16, color: AppColours.inkSecondary),
          const SizedBox(width: 6),
          Flexible(
            child: Text(
              label,
              style: AppTextStyles.caption.copyWith(
                fontWeight: FontWeight.w400,
                height: 18 / 13,
              ),
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      ),
    );
  }
}

/// Delete and "Confirm as request" for a "maybe" email. While [busy] the
/// confirm label shimmers instead of spinning.
class _MaybeActions extends StatelessWidget {
  final VoidCallback onConfirm;
  final VoidCallback onDelete;
  final bool busy;

  const _MaybeActions({
    required this.onConfirm,
    required this.onDelete,
    required this.busy,
  });

  static const _height = 48.0;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final shape = RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(14),
    );

    return Row(
      children: [
        SizedBox(
          height: _height,
          child: OutlinedButton(
            onPressed: busy ? null : onDelete,
            style: OutlinedButton.styleFrom(
              foregroundColor: AppColours.red,
              side: const BorderSide(color: AppColours.red),
              shape: shape,
            ),
            child: Text(
              t.deleteRequest,
              style: AppTextStyles.buttonLabel.copyWith(color: AppColours.red),
            ),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: SizedBox(
            height: _height,
            child: FilledButton(
              onPressed: busy ? null : onConfirm,
              style: FilledButton.styleFrom(
                backgroundColor: AppColours.primaryColor,
                disabledBackgroundColor: AppColours.primaryColor,
                shape: shape,
              ),
              child: Skeletonizer(
                enabled: busy,
                child: Text(
                  t.confirmRequest,
                  style: AppTextStyles.buttonLabel.copyWith(
                    color: Colors.white,
                  ),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}

/// New items get a CTA: filled ("Review & quote") for an untouched
/// request, outlined ("Continue quote") when a draft is already started.
class _ReviewCta extends StatelessWidget {
  final bool primary;

  const _ReviewCta({required this.primary});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final label = primary ? t.reviewAndQuote : t.continueQuote;
    const height = 48.0;
    final child = Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Text(
          label,
          style: AppTextStyles.buttonLabel.copyWith(
            color: primary ? Colors.white : AppColours.ink,
          ),
        ),
        if (primary) ...[
          const SizedBox(width: 8),
          const Icon(
            Icons.arrow_forward_rounded,
            size: 18,
            color: Colors.white,
          ),
        ],
      ],
    );

    // Purely visual: the whole card already navigates on tap.
    return IgnorePointer(
      child: SizedBox(
        height: height,
        width: double.infinity,
        child: primary
            ? FilledButton(
                onPressed: null,
                style: FilledButton.styleFrom(
                  backgroundColor: AppColours.primaryColor,
                  disabledBackgroundColor: AppColours.primaryColor,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(14),
                  ),
                ),
                child: child,
              )
            : OutlinedButton(
                onPressed: null,
                style: OutlinedButton.styleFrom(
                  backgroundColor: Colors.white,
                  disabledForegroundColor: AppColours.ink,
                  side: const BorderSide(color: AppColours.border),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(14),
                  ),
                ),
                child: child,
              ),
      ),
    );
  }
}
