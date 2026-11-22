
import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/utils/file_size.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../machines/domain/entities/job_machines.dart';
import '../../domain/entities/job_report.dart';
import '../../domain/entities/job_report_failure.dart';
import '../bloc/job_reports_cubit.dart';
import 'job_report_labels.dart';

/// A job's Reports: the files the inspector attached while inspecting. Anyone
/// who can see them can open (save) one or all of them in a ZIP; the API's
/// flags say who may add a file (`canUpload`) and who may remove each one
/// (`canDelete`).
///
/// The section loads its own list. Callers show it only when the job has
/// reports or the user may add one (`JobMachines.reportCount` /
/// `canUploadReports`); a job whose reports the user can't read yet (a
/// coordinator before approval) draws nothing.
class JobReportsSection extends StatelessWidget {
  final String requestId;

  /// The job's machines, offered when a file is added to link it to one.
  final List<MachineSummary> machines;

  const JobReportsSection({super.key, required this.requestId, this.machines = const []});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      key: ValueKey(requestId),
      create: (_) => getIt<JobReportsCubit>(param1: requestId)..start(),
      child: _SectionView(machines: machines),
    );
  }
}

class _SectionView extends StatelessWidget {
  final List<MachineSummary> machines;

  const _SectionView({required this.machines});

  /// Saves [bytes] as [fileName]: the system's save dialog on a phone or
  /// desktop, a download in the browser.
  Future<void> _save(BuildContext context, Uint8List bytes, String fileName) async {
    final t = AppLocalizations.of(context)!;
    try {
      final path = await FilePicker.platform.saveFile(fileName: fileName, bytes: bytes);
      if (path != null || kIsWeb) MToast.showSuccess(message: t.reportsSaved);
    } catch (_) {
      MToast.showError(message: t.reportsSaveFailed);
    }
  }

  Future<void> _open(BuildContext context, JobReport report) async {
    final t = AppLocalizations.of(context)!;
    final result = await context.read<JobReportsCubit>().download(report);
    if (!context.mounted) return;
    if (result.failure != null) {
      MToast.showError(message: result.failure!.message(t));
    } else if (result.bytes != null) {
      await _save(context, result.bytes!, report.fileName);
    }
  }

  Future<void> _downloadAll(BuildContext context) async {
    final t = AppLocalizations.of(context)!;
    final result = await context.read<JobReportsCubit>().downloadAll();
    if (!context.mounted) return;
    if (result.failure != null) {
      MToast.showError(message: result.failure!.message(t));
    } else if (result.bytes != null) {
      await _save(context, result.bytes!, 'reports-${context.read<JobReportsCubit>().requestId}.zip');
    }
  }

  Future<void> _remove(BuildContext context, JobReport report) async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<JobReportsCubit>();
    final confirmed = await showAdaptiveDialog<bool>(
      context: context,
      builder: (context) => AlertDialog.adaptive(
        title: Text(t.reportsRemoveTitle),
        content: Text(t.reportsRemoveMessage(report.fileName)),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(false), child: Text(t.cancel)),
          TextButton(onPressed: () => Navigator.of(context).pop(true), child: Text(t.reportsRemoveAction)),
        ],
      ),
    );
    if (confirmed != true) return;
    final failure = await cubit.remove(report);
    if (failure == null) {
      MToast.showSuccess(message: t.reportsRemoved);
    } else {
      MToast.showError(message: failure.message(t));
    }
  }

  /// Picks a file, asks which machine it is for (when the job has several) and
  /// uploads it.
  Future<void> _add(BuildContext context) async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<JobReportsCubit>();
    final limits = cubit.state.list;

    final FilePickerResult? picked;
    try {
      picked = await FilePicker.platform.pickFiles(withData: true);
    } catch (_) {
      MToast.showError(message: t.reportsErrorUnreadable);
      return;
    }
    final file = picked?.files.firstOrNull;
    if (file == null || !context.mounted) return;
    final bytes = file.bytes;
    if (bytes == null) {
      MToast.showError(message: t.reportsErrorUnreadable);
      return;
    }

    String? requestItemId;
    if (machines.length > 1) {
      final choice = await showModalBottomSheet<_MachineChoice>(
        context: context,
        isScrollControlled: true,
        backgroundColor: Colors.transparent,
        builder: (_) => _MachineSheet(machines: machines),
      );
      if (choice == null) return; // dismissed: nothing is uploaded
      requestItemId = choice.requestItemId;
    }

    final failure = await cubit.upload(fileName: file.name, bytes: bytes, requestItemId: requestItemId);
    if (failure == null) {
      MToast.showSuccess(message: t.reportsAdded);
    } else {
      MToast.showError(message: failure.message(t, limits: limits));
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return BlocBuilder<JobReportsCubit, JobReportsState>(
      builder: (context, state) {
        final list = state.list;

        if (state.status == JobReportsStatus.error) {
          final code = state.loadFailure?.code;
          // Nothing to show: not readable yet, or not there.
          if (code == JobReportFailureCode.notApproved ||
              code == JobReportFailureCode.notFound ||
              code == JobReportFailureCode.permissionDenied) {
            return const SizedBox.shrink();
          }
          return MCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(t.reportsTitle, style: AppTextStyles.sectionTitle),
                const SizedBox(height: 12),
                MNotice(type: MNoticeType.error, message: t.reportsLoadError),
                const SizedBox(height: 12),
                OutlinedButton(onPressed: context.read<JobReportsCubit>().retry, child: Text(t.retry)),
              ],
            ),
          );
        }

        if (list == null) {
          return MCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(t.reportsTitle, style: AppTextStyles.sectionTitle),
                const SizedBox(height: 14),
                const ClipRRect(
                  borderRadius: BorderRadius.all(Radius.circular(999)),
                  child: LinearProgressIndicator(minHeight: 4, color: AppColours.primaryColor),
                ),
              ],
            ),
          );
        }

        // A user who can neither add nor see anything has no use for the card.
        if (list.files.isEmpty && !list.canUpload && list.expiresAt == null) {
          return const SizedBox.shrink();
        }

        final locale = Localizations.localeOf(context).languageCode;
        return MCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      list.files.isEmpty ? t.reportsTitle : '${t.reportsTitle} · ${list.files.length}',
                      style: AppTextStyles.sectionTitle,
                    ),
                  ),
                  if (list.canUpload)
                    TextButton.icon(
                      onPressed: state.isBusy ? null : () => _add(context),
                      icon: state.isUploading
                          ? const SizedBox.square(dimension: 16, child: CircularProgressIndicator(strokeWidth: 2))
                          : const Icon(Icons.upload_file_rounded, size: 20),
                      label: Text(t.reportsAddAction),
                    ),
                ],
              ),
              if (list.canUpload) ...[
                const SizedBox(height: 2),
                Text(
                  t.reportsLimitsHint(
                    list.maxFiles,
                    list.maxFileBytes ~/ (1024 * 1024),
                    list.maxTotalBytes ~/ (1024 * 1024),
                  ),
                  style: AppTextStyles.caption.copyWith(fontSize: 12),
                ),
              ],
              const SizedBox(height: 12),
              if (list.files.isEmpty)
                Text(list.canUpload ? t.reportsEmpty : t.reportsEmptyReadOnly, style: AppTextStyles.caption)
              else
                for (final (index, report) in list.files.indexed) ...[
                  if (index > 0) const Divider(height: 20, color: AppColours.borderSubtle),
                  _FileRow(
                    report: report,
                    locale: locale,
                    busy: state.busyFileId == report.id,
                    enabled: !state.isBusy,
                    onOpen: () => _open(context, report),
                    onRemove: report.canDelete ? () => _remove(context, report) : null,
                  ),
                ],
              if (list.files.isNotEmpty) ...[
                const SizedBox(height: 12),
                OutlinedButton.icon(
                  onPressed: state.isBusy ? null : () => _downloadAll(context),
                  icon: state.isDownloadingAll
                      ? const SizedBox.square(dimension: 16, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Icon(Icons.folder_zip_outlined, size: 20),
                  label: Text(t.reportsDownloadAllAction),
                ),
              ],
              if (list.expiresAt != null) ...[
                const SizedBox(height: 12),
                MNotice(
                  type: MNoticeType.info,
                  message: t.reportsExpiry(DateFormat.yMMMd(locale).format(list.expiresAt!.toLocal())),
                ),
              ],
            ],
          ),
        );
      },
    );
  }
}

/// One file of the list: its icon, name, size and machine, who added it and
/// when. Tapping it saves the file; the bin shows only when the API allows
/// removing it.
class _FileRow extends StatelessWidget {
  final JobReport report;
  final String locale;
  final bool busy;
  final bool enabled;
  final VoidCallback onOpen;
  final VoidCallback? onRemove;

  const _FileRow({
    required this.report,
    required this.locale,
    required this.busy,
    required this.enabled,
    required this.onOpen,
    required this.onRemove,
  });

  static IconData _iconOf(String fileName) {
    final dot = fileName.lastIndexOf('.');
    return switch (dot < 0 ? '' : fileName.substring(dot + 1).toLowerCase()) {
      'png' || 'jpg' || 'jpeg' || 'gif' => Icons.image_outlined,
      'xls' || 'xlsx' || 'csv' => Icons.table_chart_outlined,
      'pdf' => Icons.picture_as_pdf_outlined,
      'ppt' || 'pptx' => Icons.slideshow_outlined,
      _ => Icons.description_outlined,
    };
  }

  @override
  Widget build(BuildContext context) {
    final uploadedAt = report.uploadedAt?.toLocal();
    final added = [
      if (report.uploadedBy != null) report.uploadedBy!,
      if (uploadedAt != null) DateFormat.yMMMd(locale).add_Hm().format(uploadedAt),
    ].join(' · ');
    final size = [formatFileSize(report.sizeBytes), if (report.machineName != null) report.machineName!].join(' · ');

    return Row(
      children: [
        Expanded(
          child: InkWell(
            onTap: enabled ? onOpen : null,
            borderRadius: BorderRadius.circular(10),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    width: 40,
                    height: 40,
                    decoration: BoxDecoration(
                      color: AppColours.primaryTint,
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: busy
                        ? const Padding(padding: EdgeInsets.all(11), child: CircularProgressIndicator(strokeWidth: 2))
                        : Icon(_iconOf(report.fileName), size: 22, color: AppColours.primaryDark),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          report.fileName,
                          style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600, color: AppColours.primaryDark),
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                        ),
                        Text(size, style: AppTextStyles.caption.copyWith(fontSize: 12), overflow: TextOverflow.ellipsis),
                        if (added.isNotEmpty)
                          Text(added, style: AppTextStyles.caption.copyWith(fontSize: 12), overflow: TextOverflow.ellipsis),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
        if (onRemove != null)
          IconButton(
            onPressed: enabled ? onRemove : null,
            tooltip: AppLocalizations.of(context)!.reportsRemoveTooltip,
            icon: const Icon(Icons.delete_outline_rounded, color: AppColours.errorIcon),
          ),
      ],
    );
  }
}

/// What the machine sheet resolves with: a machine's id, or null for the job as
/// a whole (a dismissed sheet resolves with no choice at all).
class _MachineChoice {
  final String? requestItemId;

  const _MachineChoice(this.requestItemId);
}

/// Asks which machine a report is for, with "Whole job" first.
class _MachineSheet extends StatelessWidget {
  final List<MachineSummary> machines;

  const _MachineSheet({required this.machines});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      child: SafeArea(
        child: ConstrainedBox(
          constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.7),
          child: ListView(
            shrinkWrap: true,
            padding: const EdgeInsets.fromLTRB(20, 20, 20, 12),
            children: [
              Text(t.reportsMachineSheetTitle, style: AppTextStyles.sectionTitle),
              const SizedBox(height: 4),
              Text(t.reportsMachineSheetHint, style: AppTextStyles.caption),
              const SizedBox(height: 8),
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.work_outline_rounded, color: AppColours.primaryDark),
                title: Text(t.reportsWholeJob, style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600)),
                onTap: () => Navigator.of(context).pop(const _MachineChoice(null)),
              ),
              for (final machine in machines)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(Icons.precision_manufacturing_outlined, color: AppColours.primaryDark),
                  title: Text(machine.name, style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600)),
                  subtitle: machine.serialNumber == null ? null : Text(machine.serialNumber!, style: AppTextStyles.caption),
                  onTap: () => Navigator.of(context).pop(_MachineChoice(machine.requestItemId)),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
