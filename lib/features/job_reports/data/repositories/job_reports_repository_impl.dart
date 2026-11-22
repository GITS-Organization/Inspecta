import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/error/api_error_handler.dart';
import '../../domain/entities/job_report.dart';
import '../../domain/entities/job_report_failure.dart';
import '../../domain/repositories/job_reports_repository.dart';
import '../datasources/job_reports_api_client_services.dart';
import '../models/job_reports_parser.dart';

class JobReportsRepositoryImpl implements JobReportsRepository {
  static const _safeApi = SafeApi();

  final JobReportsApiClientServices _api;

  const JobReportsRepositoryImpl(this._api);

  @override
  Future<JobReportList> list(String requestId) async {
    final result = await _safeApi(
      () async => JobReportsParser.list(await _api.getReports(requestId) as Map<String, dynamic>),
      mapError: _failureOf,
    );
    return result.fold((data) => data, (f) => throw _failureFrom(f));
  }

  @override
  Future<JobReport> upload(String requestId, JobReportUpload file) async {
    final body = <String, dynamic>{
      'fileName': file.fileName,
      'contentBase64': base64Encode(file.bytes),
      if (file.requestItemId != null) 'requestItemId': file.requestItemId,
    };
    final result = await _safeApi(
      () async => JobReportsParser.file(await _api.uploadReport(requestId, body) as Map<String, dynamic>),
      mapError: _failureOf,
    );
    return result.fold((data) => data, (f) => throw _failureFrom(f));
  }

  @override
  Future<JobReportList> delete(String requestId, String fileId) async {
    final result = await _safeApi(
      () async => JobReportsParser.list(await _api.deleteReport(requestId, fileId) as Map<String, dynamic>),
      mapError: _failureOf,
    );
    return result.fold((data) => data, (f) => throw _failureFrom(f));
  }

  @override
  Future<Uint8List> download(String requestId, String fileId) async {
    final result = await _safeApi(
      () async => Uint8List.fromList(await _api.downloadReport(requestId, fileId)),
      mapError: _failureOf,
    );
    return result.fold((data) => data, (f) => throw _failureFrom(f));
  }

  @override
  Future<Uint8List> downloadAll(String requestId) async {
    final result = await _safeApi(
      () async => Uint8List.fromList(await _api.downloadReportsZip(requestId)),
      mapError: _failureOf,
    );
    return result.fold((data) => data, (f) => throw _failureFrom(f));
  }

  /// The answers the reports have of their own, told by status and the body's
  /// `code`. Anything else (null) is classified like every other call.
  static JobReportFailure? _failureOf(Object error) {
    if (error is! DioException) return null;
    final status = error.response?.statusCode;
    final body = _bodyOf(error.response?.data);
    final code = body?['code'];
    final detail = body?['detail'];
    return switch ((status, code)) {
      (400, _) => JobReportFailure(JobReportFailureCode.badRequest, detail: detail is String ? detail : null),
      (403, 'not_the_reviewer') => const JobReportFailure(JobReportFailureCode.notTheReviewer),
      (403, _) => const JobReportFailure(JobReportFailureCode.permissionDenied),
      (404, _) => const JobReportFailure(JobReportFailureCode.notFound),
      (409, 'reports_locked') => const JobReportFailure(JobReportFailureCode.locked),
      (409, 'report_limit_reached') => const JobReportFailure(JobReportFailureCode.limitReached),
      (409, 'not_approved') => const JobReportFailure(JobReportFailureCode.notApproved),
      _ => null,
    };
  }

  /// A downloading call asks for bytes, so its error body arrives as bytes
  /// too: read it as the JSON it is.
  static Map<String, dynamic>? _bodyOf(Object? data) {
    try {
      final decoded = data is List<int> ? jsonDecode(utf8.decode(data)) : data;
      return decoded is Map<String, dynamic> ? decoded : null;
    } catch (_) {
      return null;
    }
  }

  static JobReportFailure _failureFrom(Object error) {
    if (error is JobReportFailure) return error;
    return JobReportFailure(switch (ApiErrorHandler.classify(error)) {
      ApiErrorKind.permissionDenied => JobReportFailureCode.permissionDenied,
      ApiErrorKind.network => JobReportFailureCode.network,
      ApiErrorKind.conflict || ApiErrorKind.unknown => JobReportFailureCode.unknown,
    });
  }
}
