import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:printing/printing.dart';

import '../../l10n/app_localizations.dart';
import '../consts/app_colors.dart';
import '../consts/app_text_styles.dart';
import '../framework/mtoast.dart';
import '../shared/m_notice.dart';
import '../shared/m_primary_button.dart';

/// A full-screen PDF preview on the app's usual background: the pages fill
/// the screen (pinch or double-tap to zoom) with a single "Download PDF"
/// button under them. Open it with [open]; [build] produces the PDF and is
/// called once — the same bytes are previewed and downloaded.
class PdfPreviewPage extends StatefulWidget {
  final String title;
  final String? subtitle;

  /// Name of the downloaded file, including `.pdf`.
  final String fileName;
  final Future<Uint8List> Function() build;

  /// Whether the Download PDF button is offered. When false the page is a
  /// view-only preview and the button bar isn't shown at all.
  final bool allowDownload;

  const PdfPreviewPage({
    super.key,
    required this.title,
    required this.fileName,
    required this.build,
    this.subtitle,
    this.allowDownload = true,
  });

  static Future<void> open(
    BuildContext context, {
    required String title,
    required String fileName,
    required Future<Uint8List> Function() build,
    String? subtitle,
    bool allowDownload = true,
  }) {
    return Navigator.of(context).push(
      MaterialPageRoute<void>(
        fullscreenDialog: true,
        builder: (_) => PdfPreviewPage(
          title: title,
          subtitle: subtitle,
          fileName: fileName,
          build: build,
          allowDownload: allowDownload,
        ),
      ),
    );
  }

  @override
  State<PdfPreviewPage> createState() => _PdfPreviewPageState();
}

class _PdfPreviewPageState extends State<PdfPreviewPage>
    with SingleTickerProviderStateMixin {
  static const _a4WidthInches = 8.27;
  static const _doubleTapZoom = 2.5;

  final _transform = TransformationController();
  final _downloadKey = GlobalKey();
  late final AnimationController _zoomAnimation;
  late final Animation<double> _zoomCurve;
  Matrix4Tween? _zoomTween;
  Offset _doubleTapAt = Offset.zero;

  bool _started = false;
  bool _loading = true;
  bool _failed = false;
  bool _isSaving = false;
  Uint8List? _bytes;
  List<ui.Image> _pages = const [];

  @override
  void initState() {
    super.initState();
    _zoomAnimation =
        AnimationController(
          vsync: this,
          duration: const Duration(milliseconds: 220),
        )..addListener(() {
          final tween = _zoomTween;
          if (tween != null) _transform.value = tween.evaluate(_zoomCurve);
        });
    _zoomCurve = CurvedAnimation(
      parent: _zoomAnimation,
      curve: Curves.easeOutCubic,
    );
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // MediaQuery is needed to pick the render resolution, so the first load
    // waits until it's available.
    if (!_started) {
      _started = true;
      _load();
    }
  }

  @override
  void dispose() {
    _zoomAnimation.dispose();
    _transform.dispose();
    for (final page in _pages) {
      page.dispose();
    }
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _failed = false;
    });
    try {
      final bytes = await widget.build();
      if (!mounted) return;

      // Enough pixels to stay sharp when zoomed in, capped to keep memory
      // reasonable (a 1800px-wide page is ~18 MB decoded).
      final screenPx =
          MediaQuery.sizeOf(context).width *
          MediaQuery.devicePixelRatioOf(context);
      final dpi = math.min(screenPx * 2.5, 1800.0) / _a4WidthInches;

      final pages = <ui.Image>[];
      await for (final raster in Printing.raster(bytes, dpi: dpi)) {
        pages.add(await raster.toImage());
      }
      if (!mounted || pages.isEmpty) {
        for (final page in pages) {
          page.dispose();
        }
        if (mounted) _showFailure();
        return;
      }

      for (final old in _pages) {
        old.dispose();
      }
      setState(() {
        _bytes = bytes;
        _pages = pages;
        _loading = false;
      });
    } catch (error, stack) {
      debugPrint('PDF preview failed: $error\n$stack');
      if (mounted) _showFailure();
    }
  }

  void _showFailure() {
    setState(() {
      _loading = false;
      _failed = true;
    });
  }

  Future<void> _download() async {
    final bytes = _bytes;
    if (!widget.allowDownload || bytes == null || _isSaving) return;
    final t = AppLocalizations.of(context)!;

    // iPad anchors its share sheet to a rect; use the download button's.
    final box = _downloadKey.currentContext?.findRenderObject() as RenderBox?;
    final bounds = box == null
        ? null
        : box.localToGlobal(Offset.zero) & box.size;

    setState(() => _isSaving = true);
    try {
      await Printing.sharePdf(
        bytes: bytes,
        filename: widget.fileName,
        bounds: bounds,
      );
    } catch (error, stack) {
      debugPrint('PDF download failed: $error\n$stack');
      MToast.showError(message: t.pdfDownloadFailedMessage);
    } finally {
      if (mounted) setState(() => _isSaving = false);
    }
  }

  /// Double-tap: zoom in around the tapped point, or back out if already
  /// zoomed.
  void _toggleZoom() {
    final Matrix4 target;
    if (_transform.value.getMaxScaleOnAxis() > 1.05) {
      target = Matrix4.identity();
    } else {
      const s = _doubleTapZoom;
      final tap = _doubleTapAt;
      final scenePoint = _transform.toScene(tap);
      final width = context.size?.width ?? 0;
      final tx = (tap.dx - s * scenePoint.dx)
          .clamp(width * (1 - s), 0.0)
          .toDouble();
      final ty = math.min(tap.dy - s * scenePoint.dy, 0.0);
      target = Matrix4(s, 0, 0, 0, 0, s, 0, 0, 0, 0, 1, 0, tx, ty, 0, 1);
    }
    _zoomTween = Matrix4Tween(begin: _transform.value, end: target);
    _zoomAnimation.forward(from: 0);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final ready = !_loading && !_failed;

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark,
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: Column(
          children: [
            _TopBar(title: widget.title, subtitle: widget.subtitle),
            Expanded(
              child: Padding(
                // Without the button bar, keep the page clear of the system
                // gesture area.
                padding: EdgeInsets.only(
                  bottom: widget.allowDownload
                      ? 0
                      : MediaQuery.paddingOf(context).bottom,
                ),
                child: _body(t),
              ),
            ),
            if (widget.allowDownload)
              Container(
                key: _downloadKey,
                padding: EdgeInsets.fromLTRB(
                  20,
                  14,
                  20,
                  14 + MediaQuery.paddingOf(context).bottom,
                ),
                decoration: const BoxDecoration(
                  color: Colors.white,
                  border: Border(top: BorderSide(color: AppColours.border)),
                ),
                child: MPrimaryButton(
                  label: t.downloadPdfAction,
                  loading: _isSaving,
                  onPressed: ready ? _download : null,
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _body(AppLocalizations t) {
    if (_loading) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const CircularProgressIndicator(),
            const SizedBox(height: 16),
            Text(t.preparingPdfMessage, style: AppTextStyles.subtitle),
          ],
        ),
      );
    }
    if (_failed) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              MNotice(
                type: MNoticeType.error,
                message: t.pdfPreviewFailedMessage,
              ),
              const SizedBox(height: 12),
              TextButton(
                onPressed: _load,
                child: Text(t.retry, style: AppTextStyles.link),
              ),
            ],
          ),
        ),
      );
    }

    return LayoutBuilder(
      builder: (context, constraints) {
        return GestureDetector(
          onDoubleTapDown: (details) => _doubleTapAt = details.localPosition,
          onDoubleTap: _toggleZoom,
          child: InteractiveViewer(
            transformationController: _transform,
            minScale: 1,
            maxScale: 5,
            constrained: false,
            child: SizedBox(
              width: constraints.maxWidth,
              child: ConstrainedBox(
                constraints: BoxConstraints(minHeight: constraints.maxHeight),
                child: Center(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 8,
                    ),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        for (var i = 0; i < _pages.length; i++) ...[
                          if (i > 0) const SizedBox(height: 12),
                          _PageSheet(image: _pages[i]),
                        ],
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        );
      },
    );
  }
}

class _TopBar extends StatelessWidget {
  final String title;
  final String? subtitle;

  const _TopBar({required this.title, required this.subtitle});

  @override
  Widget build(BuildContext context) {
    return Container(
      color: Colors.white,
      padding: EdgeInsetsDirectional.fromSTEB(
        20,
        MediaQuery.paddingOf(context).top + 12,
        20,
        16,
      ),
      child: Row(
        children: [
          // Same square button as [MBackButton], with a close icon since this
          // screen slides up over the one before it.
          IconButton(
            onPressed: () => Navigator.of(context).maybePop(),
            tooltip: MaterialLocalizations.of(context).closeButtonTooltip,
            style: IconButton.styleFrom(
              fixedSize: const Size.square(44),
              backgroundColor: AppColours.surfaceMuted,
              foregroundColor: AppColours.ink,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(14),
              ),
            ),
            icon: const Icon(Icons.close_rounded, size: 24),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                if (subtitle != null)
                  Text(
                    subtitle!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppTextStyles.caption,
                  ),
                Text(
                  title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.cardTitle.copyWith(fontSize: 18),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// One rendered PDF page: white paper with a soft shadow.
class _PageSheet extends StatelessWidget {
  final ui.Image image;

  const _PageSheet({required this.image});

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: AppColours.border),
        boxShadow: [
          BoxShadow(
            color: AppColours.ink.withValues(alpha: 0.08),
            blurRadius: 16,
            offset: const Offset(0, 6),
          ),
        ],
      ),
      child: AspectRatio(
        aspectRatio: image.width / image.height,
        child: RawImage(
          image: image,
          fit: BoxFit.fill,
          filterQuality: FilterQuality.medium,
        ),
      ),
    );
  }
}
