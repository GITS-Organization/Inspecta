import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';

/// The top row of a section card on the quote screen (scope of work, notes,
/// the email): an icon in a pale tile, the title with a one-line summary
/// under it, and [trailing] at the end (a chevron, usually).
class QuoteSectionHeader extends StatelessWidget {
  final IconData icon;
  final String title;
  final String summary;
  final Widget trailing;

  const QuoteSectionHeader({
    super.key,
    required this.icon,
    required this.title,
    required this.summary,
    required this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(
          width: 44,
          height: 44,
          decoration: BoxDecoration(
            color: AppColours.primarySoft,
            borderRadius: BorderRadius.circular(14),
          ),
          child: Icon(icon, size: 22, color: AppColours.primaryColor),
        ),
        const SizedBox(width: 14),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(title, style: AppTextStyles.cardTitle.copyWith(fontSize: 16)),
              const SizedBox(height: 2),
              Text(summary, style: AppTextStyles.caption.copyWith(height: 18 / 13), maxLines: 2, overflow: TextOverflow.ellipsis),
            ],
          ),
        ),
        const SizedBox(width: 8),
        trailing,
      ],
    );
  }
}
