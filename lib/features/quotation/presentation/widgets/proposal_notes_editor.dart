import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/proposal_terms.dart';

/// The notes printed under the proposal's budget table, as a card that stays
/// folded until the coordinator wants to look: the standard terms are right
/// for most quotes, and twelve open text boxes would bury the price fields.
///
/// Unfolded, each note is a numbered row — tap it to edit in place, drag the
/// handle to reorder, ✕ to remove — with "Add a note" and a reset to the
/// standard list underneath.
class ProposalNotesEditor extends StatefulWidget {
  final List<String> initial;
  final ValueChanged<List<String>> onChanged;

  const ProposalNotesEditor({super.key, required this.initial, required this.onChanged});

  @override
  State<ProposalNotesEditor> createState() => _ProposalNotesEditorState();
}

class _Note {
  final int id;
  String text;

  _Note(this.id, this.text);
}

class _ProposalNotesEditorState extends State<ProposalNotesEditor> {
  late final List<_Note> _notes;
  int _nextId = 0;
  bool _expanded = false;

  /// The note to open for editing as soon as it is built (a new one).
  int? _editNext;

  @override
  void initState() {
    super.initState();
    _notes = [for (final text in widget.initial) _Note(_nextId++, text)];
  }

  bool get _isStandard {
    if (_notes.length != defaultProposalNotes.length) return false;
    for (var i = 0; i < _notes.length; i++) {
      if (_notes[i].text != defaultProposalNotes[i]) return false;
    }
    return true;
  }

  void _notify() => widget.onChanged([for (final note in _notes) note.text]);

  void _edit(_Note note, String text) {
    note.text = text;
    _notify();
  }

  void _remove(_Note note) {
    setState(() => _notes.remove(note));
    _notify();
  }

  void _add() {
    final note = _Note(_nextId++, '');
    setState(() {
      _notes.add(note);
      _editNext = note.id;
    });
    _notify();
  }

  void _reset() {
    setState(() {
      _notes
        ..clear()
        ..addAll([for (final text in defaultProposalNotes) _Note(_nextId++, text)]);
    });
    _notify();
  }

  void _reorder(int from, int to) {
    setState(() {
      if (to > from) to -= 1;
      _notes.insert(to, _notes.removeAt(from));
    });
    _notify();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final editNext = _editNext;
    _editNext = null;

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColours.border, width: 1.5),
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
                  const Icon(Icons.sticky_note_2_outlined, size: 20, color: AppColours.primaryColor),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(t.proposalNotesLabel, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
                        const SizedBox(height: 2),
                        Text(
                          _isStandard ? t.proposalNotesStandard(_notes.length) : t.proposalNotesCount(_notes.length),
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
              child: Text(t.proposalNotesHelp, style: AppTextStyles.caption),
            ),
            ReorderableListView.builder(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              buildDefaultDragHandles: false,
              padding: const EdgeInsets.fromLTRB(8, 8, 8, 0),
              itemCount: _notes.length,
              onReorder: _reorder,
              itemBuilder: (context, index) {
                final note = _notes[index];
                return _NoteRow(
                  key: ValueKey(note.id),
                  index: index,
                  text: note.text,
                  startEditing: note.id == editNext,
                  onChanged: (text) => _edit(note, text),
                  onCommitted: () {
                    if (note.text.trim().isEmpty) _remove(note);
                  },
                  onRemove: () => _remove(note),
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
                    label: Text(t.addNote),
                  ),
                  const Spacer(),
                  if (!_isStandard)
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
  }
}

/// One note: read-only text until tapped, then a field in its place.
class _NoteRow extends StatefulWidget {
  final int index;
  final String text;
  final bool startEditing;
  final ValueChanged<String> onChanged;
  final VoidCallback onCommitted;
  final VoidCallback onRemove;

  const _NoteRow({
    super.key,
    required this.index,
    required this.text,
    required this.startEditing,
    required this.onChanged,
    required this.onCommitted,
    required this.onRemove,
  });

  @override
  State<_NoteRow> createState() => _NoteRowState();
}

class _NoteRowState extends State<_NoteRow> {
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
