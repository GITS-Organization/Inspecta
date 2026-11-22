part of 'job_reports_cubit.dart';

enum JobReportsStatus { loading, ready, error }

class JobReportsState extends Equatable {
  final JobReportsStatus status;

  /// The job's reports; null until the first load.
  final JobReportList? list;
  final JobReportFailure? loadFailure;
  final bool isUploading;
  final bool isDownloadingAll;

  /// The file being removed or downloaded.
  final String? busyFileId;

  const JobReportsState({
    this.status = JobReportsStatus.loading,
    this.list,
    this.loadFailure,
    this.isUploading = false,
    this.isDownloadingAll = false,
    this.busyFileId,
  });

  /// One action at a time.
  bool get isBusy => isUploading || isDownloadingAll || busyFileId != null;

  JobReportsState copyWith({
    JobReportsStatus? status,
    JobReportList? list,
    JobReportFailure? loadFailure,
    bool clearLoadFailure = false,
    bool? isUploading,
    bool? isDownloadingAll,
    String? busyFileId,
    bool clearBusyFile = false,
  }) {
    return JobReportsState(
      status: status ?? this.status,
      list: list ?? this.list,
      loadFailure: clearLoadFailure ? null : loadFailure ?? this.loadFailure,
      isUploading: isUploading ?? this.isUploading,
      isDownloadingAll: isDownloadingAll ?? this.isDownloadingAll,
      busyFileId: clearBusyFile ? null : busyFileId ?? this.busyFileId,
    );
  }

  @override
  List<Object?> get props => [status, list, loadFailure, isUploading, isDownloadingAll, busyFileId];
}
