import 'package:flutter/material.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../l10n/app_localizations.dart';
import '../consts/app_colors.dart';
import '../consts/app_text_styles.dart';
import '../shared/m_notice.dart';

/// A bottom sheet to pick one of a list loaded on opening — a client, one of a
/// client's machines. The list is searchable when [searchHint] is given, and
/// shimmers while it loads. Resolves with the picked item, or null when the
/// sheet is dismissed. Open it with [show].
class PickerSheet<T> extends StatefulWidget {
  final String title;
  final String? searchHint;
  final String emptyText;
  final Future<List<T>> Function() load;
  final String Function(T item) titleOf;
  final String? Function(T item)? subtitleOf;

  /// Whether [item] matches the search [query] (lower-cased, trimmed). Without
  /// it the item's title is searched.
  final bool Function(T item, String query)? matches;

  const PickerSheet({
    super.key,
    required this.title,
    required this.emptyText,
    required this.load,
    required this.titleOf,
    this.subtitleOf,
    this.searchHint,
    this.matches,
  });

  static Future<T?> show<T>(
    BuildContext context, {
    required String title,
    required String emptyText,
    required Future<List<T>> Function() load,
    required String Function(T item) titleOf,
    String? Function(T item)? subtitleOf,
    String? searchHint,
    bool Function(T item, String query)? matches,
  }) {
    return showModalBottomSheet<T>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => PickerSheet<T>(
        title: title,
        emptyText: emptyText,
        load: load,
        titleOf: titleOf,
        subtitleOf: subtitleOf,
        searchHint: searchHint,
        matches: matches,
      ),
    );
  }

  @override
  State<PickerSheet<T>> createState() => _PickerSheetState<T>();
}

class _PickerSheetState<T> extends State<PickerSheet<T>> {
  List<T>? _items;
  bool _failed = false;
  String _query = '';

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _items = null;
      _failed = false;
    });
    try {
      final items = await widget.load();
      if (mounted) setState(() => _items = items);
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    }
  }

  List<T> get _visible {
    final items = _items ?? const [];
    final query = _query.trim().toLowerCase();
    if (query.isEmpty) return items;
    final matches = widget.matches ?? (T item, String q) => widget.titleOf(item).toLowerCase().contains(q);
    return [for (final item in items) if (matches(item, query)) item];
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final loading = _items == null && !_failed;
    final visible = _visible;

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: Container(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 24),
        constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.8),
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Center(
              child: Container(
                width: 40,
                height: 4,
                margin: const EdgeInsets.only(bottom: 16),
                decoration: BoxDecoration(color: AppColours.border, borderRadius: BorderRadius.circular(999)),
              ),
            ),
            Text(widget.title, style: AppTextStyles.cardTitle.copyWith(fontSize: 18)),
            if (widget.searchHint != null) ...[
              const SizedBox(height: 12),
              TextField(
                onChanged: (value) => setState(() => _query = value),
                style: AppTextStyles.input.copyWith(fontWeight: FontWeight.w400, fontSize: 15),
                decoration: InputDecoration(
                  isDense: true,
                  hintText: widget.searchHint,
                  prefixIcon: const Icon(Icons.search_rounded, size: 20),
                  contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(12),
                    borderSide: const BorderSide(color: AppColours.border, width: 1.5),
                  ),
                ),
              ),
            ],
            const SizedBox(height: 12),
            Flexible(
              child: _failed
                  ? Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        MNotice(type: MNoticeType.error, message: t.actionFailed),
                        const SizedBox(height: 12),
                        OutlinedButton(onPressed: _load, child: Text(t.retry)),
                      ],
                    )
                  : !loading && visible.isEmpty
                  ? Padding(
                      padding: const EdgeInsets.symmetric(vertical: 24),
                      child: Center(child: Text(widget.emptyText, style: AppTextStyles.subtitle)),
                    )
                  : Skeletonizer(
                      enabled: loading,
                      child: ListView.separated(
                        shrinkWrap: true,
                        physics: loading ? const NeverScrollableScrollPhysics() : null,
                        itemCount: loading ? 4 : visible.length,
                        separatorBuilder: (_, _) => const Divider(height: 1, color: AppColours.borderSubtle),
                        itemBuilder: (context, index) {
                          if (loading) {
                            return const ListTile(title: Text('Client name here'), subtitle: Text('Details line'));
                          }
                          final item = visible[index];
                          final subtitle = widget.subtitleOf?.call(item);
                          return ListTile(
                            contentPadding: EdgeInsets.zero,
                            title: Text(
                              widget.titleOf(item),
                              style: AppTextStyles.cardTitle.copyWith(fontSize: 15),
                              overflow: TextOverflow.ellipsis,
                            ),
                            subtitle: subtitle == null || subtitle.isEmpty
                                ? null
                                : Text(subtitle, style: AppTextStyles.caption, overflow: TextOverflow.ellipsis),
                            onTap: () => Navigator.of(context).pop(item),
                          );
                        },
                      ),
                    ),
            ),
          ],
        ),
      ),
    );
  }
}
