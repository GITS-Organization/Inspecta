import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';

/// A list of points printed on the proposal — the scope of work, or the notes
/// under the budget table — as a card that can be folded away.
///
/// Unfolded, each point is a numbered row — tap it to edit in place, drag the
/// handle to reorder, ✕ to remove — with an "add" button underneath. When
/// [standard] is given (the notes' default terms), the card starts folded,
/// says whether the list is still the standard one, and offers a reset to it;
/// without it (the scope of work, which starts empty) the card starts open.
class ProposalPointsEditor extends StatefulWidget {
  final IconData icon;
  final String label;
  final String help;
  final String addLabel;
  final List<String> initial;
  final ValueChanged<List<String>> onChanged;

  /// The standard list this one resets to, when there is one.
  final List<String>? standard;

  /// Shown under the card, which turns red, when the list is not acceptable.
  final String? errorText;

  /// The folded card's second line, from the number of points and whether
  /// they are still the [standard] ones.
  final String Function(int count, bool isStandard) summaryOf;

  const ProposalPointsEditor({
    super.key,
    required this.icon,
    required this.label,
    required this.help,
    required this.addLabel,
    required this.initial,
    required this.onChanged,
    required this.summaryOf,
    this.standard,
    this.errorText,
  });

  @override
  State<ProposalPointsEditor> createState() => _ProposalPointsEditorState();
}

class _Point {
  final int id;
  String text;

  _Point(this.id, this.text);
}

class _ProposalPointsEditorState extends State<ProposalPointsEditor> {
  late final List<_Point> _points;
  int _nextId = 0;
  late bool _expanded = widget.standard == null;

  /// The point to open for editing as soon as it is built (a new one).
  int? _editNext;

  @override
  void initState() {
    super.initState();
    _points = [for (final text in widget.initial) _Point(_nextId++, text)];
  }

  bool get _isStandard {
    final standard = widget.standard;
    if (standard == null || _points.length != standard.length) return false;
    for (var i = 0; i < _points.length; i++) {
      if (_points[i].text != standard[i]) return false;
    }
    return true;
  }

  void _notify() => widget.onChanged([for (final point in _points) point.text]);

  void _edit(_Point point, String text) {
    point.text = text;
    _notify();
  }

  void _remove(_Point point) {
    setState(() => _points.remove(point));
    _notify();
  }

  void _add() {
    final point = _Point(_nextId++, '');
    setState(() {
      _points.add(point);
      _editNext = point.id;
    });
    _notify();
  }

  void _reset() {
    setState(() {
      _points
        ..clear()
        ..addAll([for (final text in widget.standard ?? const <String>[]) _Point(_nextId++, text)]);
    });
    _notify();
  }

  void _reorder(int from, int to) {
    setState(() {
      if (to > from) to -= 1;
      _points.insert(to, _points.removeAt(from));
    });
    _notify();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final editNext = _editNext;
    _editNext = null;

    final hasError = widget.errorText != null;
    final card = Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: hasError ? AppColours.errorIcon : AppColours.border, width: 1.5),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          InkWell(
            onTap: () => setState(() => _expanded = !_expanded),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(14, 12, 10, 12),
              child: Row(
                children: [
                  Icon(widget.icon, size: 20, color: AppColours.primaryColor),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(widget.label, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
                        const SizedBox(height: 2),
                        Text(
                          widget.summaryOf(_points.length, _isStandard),
                          style: AppTextStyles.caption,
                        ),
                      ],
                    ),
                  ),
                  AnimatedRotation(
                    turns: _expanded ? 0.5 : 0,
                    duration: const Duration(milliseconds: 180),
                    child: const Icon(Icons.expand_more_rounded, color: AppColours.inkMuted),
                  ),
                ],
              ),
            ),
          ),
          if (_expanded) ...[
            const Divider(height: 1, color: AppColours.border),
            Padding(
              padding: const EdgeInsets.fromLTRB(14, 10, 14, 0),
              child: Text(widget.help, style: AppTextStyles.caption),
            ),
            ReorderableListView.builder(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              buildDefaultDragHandles: false,
              padding: const EdgeInsets.fromLTRB(8, 8, 8, 0),
              itemCount: _points.length,
              onReorder: _reorder,
              itemBuilder: (context, index) {
                final point = _points[index];
                return _PointRow(
                  key: ValueKey(point.id),
                  index: index,
                  text: point.text,
                  startEditing: point.id == editNext,
                  onChanged: (text) => _edit(point, text),
                  onCommitted: () {
                    if (point.text.trim().isEmpty) _remove(point);
                  },
                  onRemove: () => _remove(point),
                );
              },
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(8, 2, 8, 6),
              child: Row(
                children: [
                  TextButton.icon(
                    onPressed: _add,
                    icon: const Icon(Icons.add_rounded, size: 18),
                    label: Text(widget.addLabel),
                  ),
                  const Spacer(),
                  if (widget.standard != null && !_isStandard)
                    TextButton(
                      onPressed: _reset,
                      style: TextButton.styleFrom(foregroundColor: AppColours.inkMuted),
                      child: Text(t.resetNotes),
                    ),
                ],
              ),
            ),
          ],
        ],
      ),
    );

    if (!hasError) return card;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        card,
        const SizedBox(height: 6),
        Text(
          widget.errorText!,
          style: AppTextStyles.caption.copyWith(
            color: AppColours.errorIcon,
            fontWeight: FontWeight.w600,
          ),
        ),
      ],
    );
  }
}

/// One note: read-only text until tapped, then a field in its place.
class _PointRow extends StatefulWidget {
  final int index;
  final String text;
  final bool startEditing;
  final ValueChanged<String> onChanged;
  final VoidCallback onCommitted;
  final VoidCallback onRemove;

  const _PointRow({
    super.key,
    required this.index,
    required this.text,
    required this.startEditing,
    required this.onChanged,
    required this.onCommitted,
    required this.onRemove,
  });

  @override
  State<_PointRow> createState() => _PointRowState();
}

class _PointRowState extends State<_PointRow> {
  late final TextEditingController _controller = TextEditingController(text: widget.text);
  final _focus = FocusNode();
  late bool _editing = widget.startEditing;

  @override
  void initState() {
    super.initState();
    _focus.addListener(() {
      if (!_focus.hasFocus && _editing) {
        setState(() => _editing = false);
        widget.onCommitted();
      }
    });
    if (_editing) _focusSoon();
  }

  void _focusSoon() => WidgetsBinding.instance.addPostFrameCallback((_) {
    if (mounted) _focus.requestFocus();
  });

  @override
  void dispose() {
    _controller.dispose();
    _focus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return Container(
      margin: const EdgeInsets.only(bottom: 6),
      decoration: BoxDecoration(
        color: _editing ? Colors.white : AppColours.background,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(
          color: _editing ? AppColours.primaryColor : Colors.transparent,
          width: _editing ? 1.5 : 0,
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ReorderableDragStartListener(
            index: widget.index,
            child: const Padding(
              padding: EdgeInsets.fromLTRB(8, 10, 4, 10),
              child: Icon(Icons.drag_indicator_rounded, size: 20, color: AppColours.inkMuted),
            ),
          ),
          Container(
            width: 22,
            height: 22,
            margin: const EdgeInsets.only(top: 9, right: 10),
            alignment: Alignment.center,
            decoration: const BoxDecoration(color: AppColours.primaryColor, shape: BoxShape.circle),
            child: Text(
              '${widget.index + 1}',
              style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: Colors.white),
            ),
          ),
          Expanded(
            child: _editing
                ? Padding(
                    padding: const EdgeInsets.symmetric(vertical: 4),
                    child: TextField(
                      controller: _controller,
                      focusNode: _focus,
                      onChanged: widget.onChanged,
                      minLines: 1,
                      maxLines: 8,
                      style: AppTextStyles.input.copyWith(fontWeight: FontWeight.w400, fontSize: 13),
                      decoration: const InputDecoration(
                        isDense: true,
                        border: InputBorder.none,
                        contentPadding: EdgeInsets.symmetric(vertical: 6),
                      ),
                    ),
                  )
                : InkWell(
                    borderRadius: BorderRadius.circular(8),
                    onTap: () {
                      setState(() => _editing = true);
                      _focusSoon();
                    },
                    child: Padding(
                      padding: const EdgeInsets.symmetric(vertical: 11),
                      child: Text(
                        _controller.text,
                        maxLines: 3,
                        overflow: TextOverflow.ellipsis,
                        style: AppTextStyles.input.copyWith(fontWeight: FontWeight.w400, fontSize: 13),
                      ),
                    ),
                  ),
          ),
          IconButton(
            onPressed: widget.onRemove,
            tooltip: t.removeNote,
            visualDensity: VisualDensity.compact,
            icon: const Icon(Icons.close_rounded, size: 18, color: AppColours.inkMuted),
          ),
        ],
      ),
    );
  }
}
