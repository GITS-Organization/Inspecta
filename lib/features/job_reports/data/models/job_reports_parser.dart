import '../../domain/entities/job_report.dart';

/// Reads the reports API's JSON into entities. Every field is optional on the
/// wire, so each reads defensively.
class JobReportsParser {
  JobReportsParser._();

  static String? _string(Object? value) => value is String && value.isNotEmpty ? value : null;

  static int? _int(Object? value) => value is num ? value.toInt() : null;

  static DateTime? _date(Object? value) => value is String ? DateTime.tryParse(value) : null;

  static JobReport file(Map<String, dynamic> json) => JobReport(
    id: _string(json['id']) ?? '',
    fileName: _string(json['fileName']) ?? '',
    contentType: _string(json['contentType']),
    sizeBytes: _int(json['sizeBytes']) ?? 0,
    requestItemId: _string(json['requestItemId']),
    machineName: _string(json['machineName']),
    uploadedBy: _string(json['uploadedBy']),
    uploadedAt: _date(json['uploadedAt']),
    canDelete: json['canDelete'] == true,
  );

  static JobReportList list(Map<String, dynamic> json) => JobReportList(
    files: [
      if (json['files'] is List)
        for (final entry in json['files'] as List)
          if (entry is Map<String, dynamic>) file(entry),
    ],
    totalBytes: _int(json['totalBytes']) ?? 0,
    canUpload: json['canUpload'] == true,
    maxFiles: _int(json['maxFiles']) ?? JobReportList.defaultMaxFiles,
    maxFileBytes: _int(json['maxFileBytes']) ?? JobReportList.defaultMaxFileBytes,
    maxTotalBytes: _int(json['maxTotalBytes']) ?? JobReportList.defaultMaxTotalBytes,
    expiresAt: _date(json['expiresAt']),
  );
}
