import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../l10n/app_localizations.dart';

/// Asks why the request is being rejected — the client is told — and resolves
/// with the reason, or null when the coordinator backs out.
Future<String?> showRejectReasonDialog(BuildContext context) {
  return showDialog<String>(
    context: context,
    builder: (_) => const _RejectReasonDialog(),
  );
}

class _RejectReasonDialog extends StatefulWidget {
  const _RejectReasonDialog();

  @override
  State<_RejectReasonDialog> createState() => _RejectReasonDialogState();
}

class _RejectReasonDialogState extends State<_RejectReasonDialog> {
  final _controller = TextEditingController();
  bool _submitted = false;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final empty = _controller.text.trim().isEmpty;

    return AlertDialog(
      title: Text(t.rejectRequestTitle),
      content: TextField(
        controller: _controller,
        autofocus: true,
        minLines: 3,
        maxLines: 5,
        textCapitalization: TextCapitalization.sentences,
        onChanged: (_) => setState(() {}),
        decoration: InputDecoration(
          labelText: t.reasonToClient,
          alignLabelWithHint: true,
          errorText: _submitted && empty ? t.errorReasonRequired : null,
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: Text(t.cancel)),
        TextButton(
          onPressed: () {
            if (empty) {
              setState(() => _submitted = true);
              return;
            }
            Navigator.of(context).pop(_controller.text.trim());
          },
          style: TextButton.styleFrom(foregroundColor: AppColours.red),
          child: Text(t.replyReject),
        ),
      ],
    );
  }
}
