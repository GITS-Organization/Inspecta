import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../l10n/app_localizations.dart';

/// What still stops the certificate being submitted, above the footer's
/// buttons.
///
/// It is a nudge, not an alarm — amber rather than red — and it must not eat
/// the form: closed it is one row (what is missing, how many, and the first
/// few things in a line), and a tap opens the full list.
class CertificateMissingPanel extends StatefulWidget {
  /// What is missing, as the API words it ("the examination date").
  final List<String> items;

  const CertificateMissingPanel({super.key, required this.items});

  @override
  State<CertificateMissingPanel> createState() => _CertificateMissingPanelState();
}

class _CertificateMissingPanelState extends State<CertificateMissingPanel> {
  static const _radius = 16.0;

  bool _open = false;

  /// The API words each item mid-sentence; as a list entry it starts a capital.
  static String _sentence(String item) {
    final text = item.trim();
    return text.isEmpty ? text : text[0].toUpperCase() + text.substring(1);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final items = widget.items.map(_sentence).where((item) => item.isNotEmpty).toList();
    if (items.isEmpty) return const SizedBox.shrink();

    final reduceMotion = MediaQuery.disableAnimationsOf(context);
    final duration = reduceMotion ? Duration.zero : const Duration(milliseconds: 220);

    // No label of its own: the texts inside are what a screen reader should read.
    return Semantics(
      button: true,
      child: Container(
        decoration: BoxDecoration(
          color: AppColours.chipAmberBackground,
          borderRadius: BorderRadius.circular(_radius),
          border: Border.all(color: AppColours.chipAmberText.withValues(alpha: 0.18)),
        ),
        child: Material(
          type: MaterialType.transparency,
          child: InkWell(
            borderRadius: BorderRadius.circular(_radius),
            onTap: () => setState(() => _open = !_open),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
              child: AnimatedSize(
                duration: duration,
                curve: Curves.easeOutCubic,
                alignment: Alignment.topCenter,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Row(
                      children: [
                        Container(
                          width: 32,
                          height: 32,
                          decoration: BoxDecoration(
                            color: Colors.white.withValues(alpha: 0.7),
                            borderRadius: BorderRadius.circular(10),
                          ),
                          child: const Icon(Icons.checklist_rounded, size: 18, color: AppColours.chipAmberText),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                t.machineMissingLabel,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  fontSize: 13,
                                  height: 1.3,
                                  fontWeight: FontWeight.w700,
                                  color: AppColours.chipAmberTextStrong,
                                ),
                              ),
                              // Closed, a glance at what the first few are.
                              if (!_open)
                                Text(
                                  items.join(' · '),
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: const TextStyle(
                                    fontSize: 12,
                                    height: 1.3,
                                    fontWeight: FontWeight.w500,
                                    color: AppColours.chipAmberText,
                                  ),
                                ),
                            ],
                          ),
                        ),
                        const SizedBox(width: 8),
                        Container(
                          constraints: const BoxConstraints(minWidth: 22),
                          height: 22,
                          padding: const EdgeInsets.symmetric(horizontal: 7),
                          alignment: Alignment.center,
                          decoration: BoxDecoration(
                            color: AppColours.chipAmberText,
                            borderRadius: BorderRadius.circular(999),
                          ),
                          child: Text(
                            '${items.length}',
                            style: const TextStyle(
                              fontSize: 12,
                              height: 1,
                              fontWeight: FontWeight.w700,
                              color: Colors.white,
                            ),
                          ),
                        ),
                        const SizedBox(width: 2),
                        AnimatedRotation(
                          turns: _open ? 0.5 : 0,
                          duration: duration,
                          curve: Curves.easeOutCubic,
                          child: const Icon(
                            Icons.keyboard_arrow_down_rounded,
                            size: 22,
                            color: AppColours.chipAmberText,
                          ),
                        ),
                      ],
                    ),
                    if (_open) ...[
                      const SizedBox(height: 8),
                      ConstrainedBox(
                        constraints: const BoxConstraints(maxHeight: 150),
                        child: SingleChildScrollView(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [for (final item in items) _Item(item)],
                          ),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// One missing thing: an empty tick-circle, then what it is.
class _Item extends StatelessWidget {
  final String text;

  const _Item(this.text);

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Padding(
            padding: EdgeInsets.only(top: 2),
            child: Icon(Icons.radio_button_unchecked_rounded, size: 15, color: AppColours.chipAmberText),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              text,
              style: const TextStyle(
                fontSize: 13,
                height: 1.35,
                fontWeight: FontWeight.w500,
                color: AppColours.chipAmberTextStrong,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
