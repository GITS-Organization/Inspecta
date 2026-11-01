import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/request_item.dart';
import 'request_section_parts.dart';

/// The equipment on a request (BR-02.6: each item is quoted and certified
/// separately), one panel per item with its facts and, when it sits somewhere
/// other than the rest of the job, its own location.
class EquipmentGrid extends StatelessWidget {
  final List<RequestItem> items;

  const EquipmentGrid({super.key, required this.items});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return RequestSection(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Expanded(child: SectionTitle(t.equipmentLabel)),
              if (items.isNotEmpty) StatusPill(label: t.itemsCount(items.length), tone: PillTone.success),
            ],
          ),
          for (var i = 0; i < items.length; i++) ...[
            SizedBox(height: i == 0 ? 14 : 12),
            _ItemPanel(item: items[i], number: i + 1, total: items.length),
          ],
        ],
      ),
    );
  }
}

/// One item: its name, whether it is one of the client's machines, its facts.
class _ItemPanel extends StatelessWidget {
  final RequestItem item;
  final int number;
  final int total;

  const _ItemPanel({required this.item, required this.number, required this.total});

  static bool _has(String? value) => value != null && value.trim().isNotEmpty;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColours.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              const IconTile(icon: Icons.precision_manufacturing_outlined, size: 44),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(t.equipmentItemOf(number, total), style: rowLabelStyle),
                    Text(
                      item.type,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.cardTitle.copyWith(fontSize: 16, height: 21 / 16),
                    ),
                  ],
                ),
              ),
            ],
          ),
          if (item.equipmentId != null) ...[
            const SizedBox(height: 12),
            MachineLinkStrip(linked: true, label: t.clientMachineLinked),
          ],
          const SizedBox(height: 14),
          ItemFacts(
            facts: [
              (t.serialNumberLabel, item.serialNumber),
              if (_has(item.manufacturer)) (t.manufacturerLabel, item.manufacturer),
              if (_has(item.model)) (t.modelYearLabel, item.model),
              if (_has(item.prNumber)) (t.prNumberLabel, item.prNumber),
            ],
          ),
          if (_has(item.location)) ...[
            const SizedBox(height: 14),
            _LocationStrip(label: t.equipmentLocationLabel, value: item.location!.trim()),
          ],
        ],
      ),
    );
  }
}

class _LocationStrip extends StatelessWidget {
  final String label;
  final String value;

  const _LocationStrip({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColours.surfaceMuted,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          const Icon(Icons.place_outlined, size: 18, color: AppColours.primaryDark),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(label, style: rowLabelStyle),
                Text(value, style: AppTextStyles.cardTitle.copyWith(fontSize: 14, height: 20 / 14)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
