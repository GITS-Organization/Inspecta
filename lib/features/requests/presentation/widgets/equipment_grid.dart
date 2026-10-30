import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/request_item.dart';

/// The equipment on a request (BR-02.6: each item is quoted and certified
/// separately), plus the request's preferred date and where things are.
///
/// One item reads as a plain set of tiles; several are split into their own
/// numbered panels, each with its own details and its own location, so it's
/// clear which data belongs to which equipment.
class EquipmentGrid extends StatelessWidget {
  final List<RequestItem> items;
  final DateTime? preferredDate;

  /// The request's site-wide location — an item's own when it has none.
  final String location;

  const EquipmentGrid({
    super.key,
    required this.items,
    this.preferredDate,
    this.location = '',
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final multiple = items.length > 1;

    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Text(t.equipmentLabel, style: AppTextStyles.sectionTitle),
              if (multiple) ...[
                const SizedBox(width: 8),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    color: AppColours.primaryTint,
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    '${items.length}',
                    style: AppTextStyles.badge.copyWith(
                      fontWeight: FontWeight.w700,
                      color: AppColours.primaryDark,
                    ),
                  ),
                ),
              ],
            ],
          ),
          const SizedBox(height: 16),
          for (var i = 0; i < items.length; i++) ...[
            if (i > 0) const SizedBox(height: 12),
            _ItemSection(
              item: items[i],
              number: i + 1,
              total: items.length,
              fallbackLocation: location,
            ),
          ],
          if (preferredDate != null) ...[
            const SizedBox(height: 12),
            _TileGrid(
              tiles: [
                _Tile(
                  label: t.preferredDateLabel,
                  value: DateFormat.yMMMd(
                    Localizations.localeOf(context).languageCode,
                  ).format(preferredDate!),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

/// One item: its tiles and its location. Wrapped in a bordered, numbered
/// panel when the request has more than one.
class _ItemSection extends StatelessWidget {
  final RequestItem item;
  final int number;
  final int total;
  final String fallbackLocation;

  const _ItemSection({
    required this.item,
    required this.number,
    required this.total,
    required this.fallbackLocation,
  });

  static bool _has(String? value) => value != null && value.trim().isNotEmpty;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final place = _has(item.location) ? item.location! : fallbackLocation;

    final body = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        _TileGrid(
          tiles: [
            _Tile(label: t.equipmentTypeLabel, value: item.type),
            if (_has(item.capacity)) _Tile(label: t.capacityLabel, value: item.capacity!),
            _Tile(label: t.quantityLabel, value: t.unitsCount(item.quantity)),
            if (_has(item.manufacturer))
              _Tile(label: t.manufacturerLabel, value: item.manufacturer!),
            if (_has(item.model)) _Tile(label: t.modelYearLabel, value: item.model!),
            if (_has(item.serialNumber))
              _Tile(label: t.serialNumberLabel, value: item.serialNumber!),
            if (_has(item.prNumber)) _Tile(label: t.prNumberLabel, value: item.prNumber!),
          ],
        ),
        if (place.isNotEmpty) ...[
          const SizedBox(height: 8),
          _LocationStrip(
            label: t.equipmentLocationLabel,
            value: place,
            icon: Icons.precision_manufacturing_outlined,
            tint: AppColours.primaryTint,
          ),
        ],
      ],
    );

    if (total == 1) return body;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColours.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Container(
                width: 28,
                height: 28,
                alignment: Alignment.center,
                decoration: const BoxDecoration(
                  color: AppColours.primaryColor,
                  shape: BoxShape.circle,
                ),
                child: Text(
                  '$number',
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: Colors.white,
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      t.equipmentItemOf(number, total),
                      style: AppTextStyles.caption.copyWith(fontSize: 12, height: 16 / 12),
                    ),
                    Text(
                      item.displayTitle,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.cardTitle,
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          body,
        ],
      ),
    );
  }
}

/// Two tiles per row, 8px apart.
class _TileGrid extends StatelessWidget {
  final List<Widget> tiles;

  const _TileGrid({required this.tiles});

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final width = (constraints.maxWidth - 8) / 2;
        return Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [for (final tile in tiles) SizedBox(width: width, child: tile)],
        );
      },
    );
  }
}

class _Tile extends StatelessWidget {
  final String label;
  final String value;

  const _Tile({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: AppColours.surfaceMuted,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(label, style: AppTextStyles.caption.copyWith(fontSize: 12, height: 16 / 12)),
          const SizedBox(height: 3),
          Text(
            value,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.emphasis.copyWith(fontSize: 14, height: 20 / 14),
          ),
        ],
      ),
    );
  }
}

class _LocationStrip extends StatelessWidget {
  final String label;
  final String value;
  final IconData icon;
  final Color tint;

  const _LocationStrip({
    required this.label,
    required this.value,
    required this.icon,
    required this.tint,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(color: tint, borderRadius: BorderRadius.circular(14)),
      child: Row(
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(icon, size: 20, color: AppColours.primaryDark),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(label, style: AppTextStyles.caption.copyWith(fontSize: 12, height: 16 / 12)),
                Text(
                  value,
                  style: AppTextStyles.subtitle.copyWith(
                    height: 20 / 14,
                    fontWeight: FontWeight.w600,
                    color: AppColours.ink,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
