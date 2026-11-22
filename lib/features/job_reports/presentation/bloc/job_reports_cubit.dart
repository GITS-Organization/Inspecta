import 'dart:typed_data';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../domain/entities/job_report.dart';
import '../../domain/entities/job_report_failure.dart';
import '../../domain/usecases/delete_job_report.dart';
import '../../domain/usecases/download_job_report.dart';
import '../../domain/usecases/download_job_reports_zip.dart';
import '../../domain/usecases/get_job_reports.dart';
import '../../domain/usecases/upload_job_report.dart';

part 'job_reports_state.dart';

/// Backs a job's Reports section: the files the inspector attached, adding
/// one, removing one and downloading them (one by one, or all in a ZIP).
///
/// What the user may do comes from the API's own flags (`canUpload`, each
/// file's `canDelete`); the cubit only checks what it can know before sending
/// — the file's type and size and the job's limits — so a refused file never
/// travels.
class JobReportsCubit extends Cubit<JobReportsState> {
  /// What the API takes, by extension. HEIC photos and ZIPs are not allowed.
  static const allowedExtensions = [
    'pdf',
    'png',
    'jpg',
    'jpeg',
    'gif',
    'txt',
    'csv',
    'doc',
    'docx',
    'xls',
    'xlsx',
    'ppt',
    'pptx',
  ];

  final String requestId;
  final GetJobReports _getReports;
  final UploadJobReport _upload;
  final DeleteJobReport _delete;
  final DownloadJobReport _download;
  final DownloadJobReportsZip _downloadZip;

  JobReportsCubit({
    required this.requestId,
    required GetJobReports getJobReports,
    required UploadJobReport uploadJobReport,
    required DeleteJobReport deleteJobReport,
    required DownloadJobReport downloadJobReport,
    required DownloadJobReportsZip downloadJobReportsZip,
  }) : _getReports = getJobReports,
       _upload = uploadJobReport,
       _delete = deleteJobReport,
       _download = downloadJobReport,
       _downloadZip = downloadJobReportsZip,
       super(const JobReportsState());

  /// Loads the list — also the reload after an action. A failed reload keeps
  /// the list already on screen; only a first load shows the error.
  Future<void> start() async {
    try {
      final list = await _getReports(requestId);
      if (isClosed) return;
      emit(state.copyWith(status: JobReportsStatus.ready, list: list, clearLoadFailure: true));
    } catch (error) {
      if (isClosed || state.status == JobReportsStatus.ready) return;
      emit(state.copyWith(status: JobReportsStatus.error, loadFailure: _failureOf(error)));
    }
  }

  Future<void> retry() {
    emit(state.copyWith(status: JobReportsStatus.loading, clearLoadFailure: true));
    return start();
  }

  /// Adds [bytes] as [fileName], for one machine of the job or (without
  /// [requestItemId]) the job as a whole. Returns why it was not added, or
  /// null once it is.
  Future<JobReportFailureCode?> upload({
    required String fileName,
    required Uint8List bytes,
    String? requestItemId,
  }) async {
    final list = state.list;
    if (list == null || state.isBusy) return null;

    final local = _checkBeforeSending(list, fileName, bytes.length);
    if (local != null) return local;

    emit(state.copyWith(isUploading: true));
    try {
      await _upload(requestId, JobReportUpload(fileName: fileName, bytes: bytes, requestItemId: requestItemId));
      if (isClosed) return null;
      await _reload();
      return null;
    } catch (error) {
      if (isClosed) return null;
      emit(state.copyWith(isUploading: false));
      return _failureOf(error).code;
    }
  }

  /// Removes [report]. Returns why it was not removed, or null once it is.
  Future<JobReportFailureCode?> remove(JobReport report) async {
    if (state.isBusy) return null;
    emit(state.copyWith(busyFileId: report.id));
    try {
      final list = await _delete(requestId, report.id);
      if (isClosed) return null;
      emit(state.copyWith(list: list, clearBusyFile: true));
      return null;
    } catch (error) {
      if (isClosed) return null;
      emit(state.copyWith(clearBusyFile: true));
      return _failureOf(error).code;
    }
  }

  /// One file's bytes, or why they could not be read.
  Future<({Uint8List? bytes, JobReportFailureCode? failure})> download(JobReport report) async {
    if (state.isBusy) return (bytes: null, failure: null);
    emit(state.copyWith(busyFileId: report.id));
    try {
      final bytes = await _download(requestId, report.id);
      return (bytes: bytes, failure: null);
    } catch (error) {
      return (bytes: null, failure: _failureOf(error).code);
    } finally {
      if (!isClosed) emit(state.copyWith(clearBusyFile: true));
    }
  }

  /// Every report in one ZIP, or why it could not be read.
  Future<({Uint8List? bytes, JobReportFailureCode? failure})> downloadAll() async {
    if (state.isBusy) return (bytes: null, failure: null);
    emit(state.copyWith(isDownloadingAll: true));
    try {
      final bytes = await _downloadZip(requestId);
      return (bytes: bytes, failure: null);
    } catch (error) {
      return (bytes: null, failure: _failureOf(error).code);
    } finally {
      if (!isClosed) emit(state.copyWith(isDownloadingAll: false));
    }
  }

  /// The list again after an upload; the upload itself succeeded, so a failed
  /// reload only leaves the old list until the next refresh.
  Future<void> _reload() async {
    try {
      final list = await _getReports(requestId);
      if (isClosed) return;
      emit(state.copyWith(list: list, isUploading: false));
    } catch (_) {
      if (!isClosed) emit(state.copyWith(isUploading: false));
    }
  }

  /// What the user can be told before anything is sent: a type the API
  /// doesn't take, an empty or oversized file, and the job's limits.
  static JobReportFailureCode? _checkBeforeSending(JobReportList list, String fileName, int size) {
    final dot = fileName.lastIndexOf('.');
    final extension = dot < 0 || dot == fileName.length - 1 ? '' : fileName.substring(dot + 1).toLowerCase();
    if (extension == 'heic' || extension == 'heif') return JobReportFailureCode.heic;
    if (!allowedExtensions.contains(extension)) return JobReportFailureCode.unsupportedType;
    if (size == 0) return JobReportFailureCode.emptyFile;
    if (size > list.maxFileBytes) return JobReportFailureCode.fileTooLarge;
    if (list.files.length >= list.maxFiles || list.totalBytes + size > list.maxTotalBytes) {
      return JobReportFailureCode.limitReached;
    }
    return null;
  }

  static JobReportFailure _failureOf(Object error) =>
      error is JobReportFailure ? error : const JobReportFailure(JobReportFailureCode.unknown);
}
