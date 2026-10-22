import 'package:flutter/material.dart';

import '../consts/app_colors.dart';
import '../consts/app_text_styles.dart';
import '../consts/svgs.dart';
import 'm_svg.dart';

/// White pill search box used on list screens (e.g. the requests inbox
/// header): a search icon, a text field, and an optional [trailing]
/// widget (a filter button, for instance).
class ISearchField extends StatelessWidget {
  final TextEditingController controller;
  final ValueChanged<String>? onChanged;
  final String? hintText;
  final Widget? trailing;

  const ISearchField({
    super.key,
    required this.controller,
    this.onChanged,
    this.hintText,
    this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 48,
      padding: const EdgeInsets.symmetric(horizontal: 16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColours.border),
      ),
      child: Row(
        children: [
          MSvg(
            name: Svgs.search,
            width: 18,
            height: 18,
            color: AppColours.inkMuted,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: TextField(
              controller: controller,
              onChanged: onChanged,
              textAlignVertical: TextAlignVertical.center,
              style: AppTextStyles.input.copyWith(fontSize: 14),
              decoration: InputDecoration(
                border: InputBorder.none,
                isCollapsed: true,
                hintText: hintText,
                hintStyle: AppTextStyles.input.copyWith(
                  fontSize: 14,
                  fontWeight: FontWeight.w400,
                  color: AppColours.inkMuted,
                ),
              ),
            ),
          ),
          if (trailing != null) ...[const SizedBox(width: 10), trailing!],
        ],
      ),
    );
  }
}
