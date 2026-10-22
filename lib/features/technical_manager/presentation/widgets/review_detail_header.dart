import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import 'review_labels.dart';

/// The review screen's banner: back button, certificate number and title,
/// and the card saying who submitted it and when.
class ReviewDetailHeader extends StatelessWidget {
  final InspectionRequest request;
  final String? inspectorName;
  final DateTime? submittedAt;

  const ReviewDetailHeader({super.key, required this.request, this.inspectorName, this.submittedAt});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    return AppBanner(
      topInset: MediaQuery.paddingOf(context).top,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              const MBackButton(),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      certNumberFor(request),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.caption.copyWith(
                        fontWeight: FontWeight.w600,
                        letterSpacing: 0.24,
                        height: 16 / 12,
                        fontSize: 12,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      t.reviewCertificateTitle,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.sectionTitle.copyWith(
                        fontSize: 22,
                        height: 28 / 22,
                        letterSpacing: -0.22,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(18),
              border: Border.all(color: AppColours.border),
            ),
            child: Row(
              children: [
                Container(
                  width: 48,
                  height: 48,
                  alignment: Alignment.center,
                  decoration: const BoxDecoration(color: AppColours.navy, shape: BoxShape.circle),
                  child: Text(
                    _initials(inspectorName),
                    style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: Colors.white),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(inspectorName ?? '—', style: AppTextStyles.cardTitle),
                      if (submittedAt != null)
                        Text(
                          t.submittedRelativeLabel(reviewWhenLabel(t, submittedAt!, locale)),
                          style: AppTextStyles.caption.copyWith(height: 18 / 13),
                        ),
                    ],
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  decoration: BoxDecoration(
                    color: AppColours.chipAmberBackground,
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(t.awaitingYouBadge, style: AppTextStyles.badge.copyWith(color: AppColours.chipAmberText)),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  static String _initials(String? name) {
    final parts = (name ?? '').trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty);
    final letters = parts.take(2).map((p) => p[0].toUpperCase()).join();
    return letters.isEmpty ? '?' : letters;
  }
}
