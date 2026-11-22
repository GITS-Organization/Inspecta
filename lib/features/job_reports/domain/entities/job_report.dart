import 'package:equatable/equatable.dart';

/// One report file an inspector attached to a job (a scanned report, a
/// photo, a spreadsheet). It belongs to the job, with an optional link to one
/// machine, and is never part of a certificate.
class JobReport extends Equatable {
  final String id;
  final String fileName;
  final String? contentType;
  final int sizeBytes;

  /// Null for the job as a whole, or when the machine was taken out of the job.
  final String? requestItemId;
  final String? machineName;
  final String? uploadedBy;
  final DateTime? uploadedAt;

  /// Whether the signed-in user may remove it now.
  final bool canDelete;

  const JobReport({
    required this.id,
    required this.fileName,
    required this.sizeBytes,
    this.contentType,
    this.requestItemId,
    this.machineName,
    this.uploadedBy,
    this.uploadedAt,
    this.canDelete = false,
  });

  @override
  List<Object?> get props => [
    id,
    fileName,
    contentType,
    sizeBytes,
    requestItemId,
    machineName,
    uploadedBy,
    uploadedAt,
    canDelete,
  ];
}

/// A job's reports with the limits and what the signed-in user may do
/// (`GET /quotation-queue/{id}/reports`).
class JobReportList extends Equatable {
  /// The limits the API reports; these are its defaults, used until it says
  /// otherwise.
  static const defaultMaxFiles = 20;
  static const defaultMaxFileBytes = 10 * 1024 * 1024;
  static const defaultMaxTotalBytes = 30 * 1024 * 1024;

  /// Oldest first.
  final List<JobReport> files;
  final int totalBytes;

  /// The signed-in user can add a file now.
  final bool canUpload;
  final int maxFiles;
  final int maxFileBytes;
  final int maxTotalBytes;

  /// Null until the job is delivered, then the moment the files are deleted.
  final DateTime? expiresAt;

  const JobReportList({
    this.files = const [],
    this.totalBytes = 0,
    this.canUpload = false,
    this.maxFiles = defaultMaxFiles,
    this.maxFileBytes = defaultMaxFileBytes,
    this.maxTotalBytes = defaultMaxTotalBytes,
    this.expiresAt,
  });

  @override
  List<Object?> get props => [files, totalBytes, canUpload, maxFiles, maxFileBytes, maxTotalBytes, expiresAt];
}

/// A file the user picked to upload.
class JobReportUpload {
  final String fileName;
  final List<int> bytes;
  final String? requestItemId;

  const JobReportUpload({required this.fileName, required this.bytes, this.requestItemId});
}
