import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/services.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../core/utils/validators.dart';
import '../../../../core/widgets/email_composer.dart';
import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../machines/presentation/widgets/machine_widgets.dart';
import '../../domain/entities/quotation_failure.dart';
import '../../domain/entities/quote_email.dart';
import '../widgets/quotation_labels.dart';

/// What the loading skeleton is drawn from; only its shape matters.
const _placeholder = QuoteEmailPreview(
  to: 'client@company.com',
  subject: 'Re: Equipment inspection request',
  body: 'Dear client,\n\nPlease find attached our quotation for the inspection.\n\nKind regards',
  canSend: true,
);

/// The last look before the client gets the quotation: the email that carries
/// the PDF — To, Cc (up to ten), subject and text — which the coordinator can
/// edit, and the attached PDF. Nothing is sent or saved here; the edits go
/// back to the reply form and out with Send (or Accept).
///
/// The email comes from the API; its signature and the quoted original email
/// are added automatically when it goes out. It is shown and editable even
/// while emailing is switched off on the API (then only an info line says so).
class QuoteEmailPage extends StatefulWidget {
  final String clientName;

  /// The email for accepting the quote rather than for sending the offer.
  final bool accept;
  final Future<QuoteEmailPreview> Function() load;
  final Future<Uint8List> Function() loadPdf;

  /// Edits made earlier, shown on top of the API's suggestion.
  final QuoteEmailEdits? initialEdits;

  const QuoteEmailPage({
    super.key,
    required this.clientName,
    required this.accept,
    required this.load,
    required this.loadPdf,
    this.initialEdits,
  });

  /// Opens the page; resolves with the whole email as it stands, or null when
  /// the coordinator went back without using it.
  static Future<QuoteEmailEdits?> open(
    BuildContext context, {
    required String clientName,
    required bool accept,
    required Future<QuoteEmailPreview> Function() load,
    required Future<Uint8List> Function() loadPdf,
    QuoteEmailEdits? initialEdits,
  }) {
    return Navigator.of(context).push<QuoteEmailEdits>(
      MaterialPageRoute<QuoteEmailEdits>(
        builder: (_) => QuoteEmailPage(
          clientName: clientName,
          accept: accept,
          load: load,
          loadPdf: loadPdf,
          initialEdits: initialEdits,
        ),
      ),
    );
  }

  @override
  State<QuoteEmailPage> createState() => _QuoteEmailPageState();
}

class _QuoteEmailPageState extends State<QuoteEmailPage> {
  QuoteEmailPreview? _preview;
  QuotationFailureCode? _failure;

  // What is in the form now (the preview's values, or the earlier edits,
  // until the coordinator types).
  String _to = '';
  List<String> _cc = const [];
  String _subject = '';
  String _body = '';

  /// The files added to the email, on top of the quotation's own PDF.
  List<QuoteAttachment> _files = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _preview = null;
      _failure = null;
    });
    try {
      final preview = await widget.load();
      if (!mounted) return;
      final edits = widget.initialEdits;
      setState(() {
        _preview = preview;
        _to = edits?.sentTo ?? preview.to ?? '';
        _cc = edits?.cc ?? preview.cc;
        _subject = edits?.subject ?? preview.subject;
        _body = edits?.message ?? preview.body;
        _files = edits?.attachments ?? const [];
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _failure = error is QuotationFailure ? error.code : QuotationFailureCode.unknown);
    }
  }

  /// The email needs an address, a subject and text, and the API must allow
  /// it — a missing address is the one blocker typing one here clears. While
  /// emailing is switched off nothing is mailed, so the email is always
  /// usable (an address, if one is typed, still has to look right).
  bool get _canUse {
    final preview = _preview;
    if (preview == null) return false;
    if (!preview.emailEnabled) return _to.trim().isEmpty || Validators.isEmail(_to);
    final blocked = !preview.canSend && (preview.to ?? '').isNotEmpty;
    return !blocked && Validators.isEmail(_to) && _subject.trim().isNotEmpty && _body.trim().isNotEmpty;
  }

  /// The whole email as it stands, edited or not: it goes out with Send in
  /// full (`sentTo`, `cc`, `subject`, `message`), not as a difference from
  /// the suggestion.
  QuoteEmailEdits _email() {
    final to = _to.trim();
    return QuoteEmailEdits(
      sentTo: to.isEmpty ? null : to,
      cc: _cc,
      subject: _subject.trim(),
      message: _body.trim(),
      attachments: _files,
    );
  }

  int get _filesSize => _files.fold(0, (total, file) => total + file.size);

  /// Lets the coordinator pick files, keeping within the number and the total
  /// size an email can carry; a file that would go over is left out and the
  /// limit is said.
  Future<void> _addFiles() async {
    final t = AppLocalizations.of(context)!;
    final FilePickerResult? picked;
    try {
      picked = await FilePicker.platform.pickFiles(allowMultiple: true, withData: true);
    } catch (_) {
      MToast.showError(message: t.emailFileReadFailed);
      return;
    }
    if (picked == null || !mounted) return;

    final files = [..._files];
    var size = _filesSize;
    String? problem;
    for (final file in picked.files) {
      final bytes = file.bytes;
      if (bytes == null) {
        problem = t.emailFileReadFailed;
      } else if (files.length >= QuoteAttachment.maxCount) {
        problem = t.emailFilesLimit(QuoteAttachment.maxCount);
      } else if (size + bytes.length > QuoteAttachment.maxTotalBytes) {
        problem = t.emailFilesTooLarge(QuoteAttachment.maxTotalBytes ~/ (1024 * 1024));
      } else {
        files.add(QuoteAttachment(fileName: file.name, bytes: bytes));
        size += bytes.length;
      }
    }
    setState(() => _files = files);
    if (problem != null) MToast.showError(message: problem);
  }

  void _removeFile(QuoteAttachment file) => setState(() => _files = [..._files]..remove(file));

  static String _sizeLabel(int bytes) {
    if (bytes < 1024) return '$bytes B';
    if (bytes < 1024 * 1024) return '${(bytes / 1024).round()} KB';
    return '${(bytes / (1024 * 1024)).toStringAsFixed(1)} MB';
  }

  Future<void> _previewPdf(QuoteEmailPreview preview) {
    final t = AppLocalizations.of(context)!;
    return PdfPreviewPage.open(
      context,
      title: t.proposalTitle,
      fileName: preview.attachmentName ?? 'proposal.pdf',
      allowDownload: false,
      build: widget.loadPdf,
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final preview = _preview;

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark,
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: Column(
          children: [
            MachinesHeader(
              eyebrow: widget.clientName,
              title: t.emailPreviewTitle,
            ),
            const SizedBox(height: 12),
            Expanded(child: _content(context, t, preview)),
            if (preview != null)
              Container(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 16),
                decoration: const BoxDecoration(
                  color: Colors.white,
                  border: Border(top: BorderSide(color: AppColours.borderSubtle)),
                ),
                child: SafeArea(
                  top: false,
                  minimum: const EdgeInsets.only(bottom: 6),
                  child: MPrimaryButton(
                    label: t.quoteEmailUseAction,
                    onPressed: _canUse ? () => Navigator.of(context).pop(_email()) : null,
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _content(BuildContext context, AppLocalizations t, QuoteEmailPreview? preview) {
    final failure = _failure;
    if (failure != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              MNotice(type: MNoticeType.error, message: failure.message(t)),
              const SizedBox(height: 16),
              OutlinedButton(onPressed: _load, child: Text(t.retry)),
            ],
          ),
        ),
      );
    }

    final loading = preview == null;
    final shown = preview ?? _placeholder;
    return Skeletonizer(
      enabled: loading,
      child: ListView(
        physics: loading ? const NeverScrollableScrollPhysics() : null,
        keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
        children: [
          // One line about the email: the API's own, or — when emailing is off
          // and the API said nothing — ours. The form below stays editable
          // either way.
          if (shown.notice ?? (shown.emailEnabled ? null : t.quoteEmailDisabledNotice) case final info?) ...[
            MNotice(type: MNoticeType.info, message: info),
            const SizedBox(height: 12),
          ],
          if (shown.blockers.isNotEmpty) ...[
            MNotice(
              type: MNoticeType.error,
              title: t.emailCannotSendTitle,
              message: shown.blockers.join('\n'),
            ),
            const SizedBox(height: 12),
          ],
          MCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (shown.from != null) ...[
                    Text(t.emailFromLabel, style: AppTextStyles.badge),
                    const SizedBox(height: 4),
                    Text(shown.from!, style: AppTextStyles.body),
                    const SizedBox(height: 16),
                  ],
                  // A new key when the real email arrives, so the fields start
                  // from it rather than from the placeholder.
                  EmailComposer(
                    key: ValueKey(loading),
                    to: loading ? (shown.to ?? '') : _to,
                    cc: loading ? shown.cc : _cc,
                    subject: loading ? shown.subject : _subject,
                    body: loading ? shown.body : _body,
                    enabled: !loading,
                    onToChanged: (value) => setState(() => _to = value),
                    onCcChanged: (value) => setState(() => _cc = value),
                    onSubjectChanged: (value) => setState(() => _subject = value),
                    onBodyChanged: (value) => setState(() => _body = value),
                  ),
                  const SizedBox(height: 12),
                  Text(t.quoteEmailSignatureHint, style: AppTextStyles.caption),
                ],
              ),
            ),
          if (shown.attachmentName != null || loading) ...[
            const SizedBox(height: 12),
            MCard(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
              child: Row(
                children: [
                  const Icon(Icons.picture_as_pdf_outlined, size: 22, color: AppColours.primaryColor),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(t.quoteEmailAttachmentLabel, style: AppTextStyles.caption.copyWith(fontSize: 12)),
                        Text(
                          shown.attachmentName ?? 'proposal.pdf',
                          style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ),
                  ),
                  TextButton(
                    onPressed: loading ? null : () => _previewPdf(shown),
                    child: Text(t.previewAction, style: AppTextStyles.link.copyWith(fontSize: 14)),
                  ),
                ],
              ),
            ),
          ],
          // More files for the client, sent after the proposal.
          const SizedBox(height: 12),
          MCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(t.emailFilesTitle, style: AppTextStyles.sectionTitle),
                const SizedBox(height: 2),
                Text(t.emailFilesHint, style: AppTextStyles.caption),
                for (final file in _files) ...[
                  const SizedBox(height: 10),
                  Container(
                    padding: const EdgeInsetsDirectional.fromSTEB(12, 4, 4, 4),
                    decoration: BoxDecoration(
                      color: AppColours.surfaceMuted,
                      borderRadius: BorderRadius.circular(14),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.attach_file_rounded, size: 20, color: AppColours.primaryColor),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                file.fileName,
                                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600, fontSize: 14),
                                overflow: TextOverflow.ellipsis,
                              ),
                              Text(_sizeLabel(file.size), style: AppTextStyles.caption.copyWith(fontSize: 12)),
                            ],
                          ),
                        ),
                        IconButton(
                          onPressed: () => _removeFile(file),
                          tooltip: t.emailRemoveFileAction,
                          icon: const Icon(Icons.close_rounded, size: 20),
                        ),
                      ],
                    ),
                  ),
                ],
                const SizedBox(height: 12),
                SizedBox(
                  width: double.infinity,
                  height: 48,
                  child: OutlinedButton.icon(
                    onPressed: loading ? null : _addFiles,
                    icon: const Icon(Icons.add_rounded, size: 20),
                    label: Text(t.emailAddFileAction),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppColours.ink,
                      side: const BorderSide(color: AppColours.border),
                      textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14, letterSpacing: 0),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                    ),
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
