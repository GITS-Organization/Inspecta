import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:json_annotation/json_annotation.dart';

part 'api_error_model.g.dart';

abstract class BaseErrorModel implements Exception {
  final dynamic message;
  final int? code;

  @JsonKey(includeFromJson: false, includeToJson: false)
  final StackTrace trace;

  BaseErrorModel({this.message, this.code, StackTrace? trace})
    : trace = trace ?? StackTrace.current {
    if (kDebugMode) debugPrint('${toString()}\n${this.trace}');
  }

  @override
  String toString() => 'Error: $message (code: $code)';
}

@JsonSerializable()
class ApiErrorModel extends BaseErrorModel {
  @JsonKey(includeFromJson: false, includeToJson: false)
  final DioException? dioException;

  ApiErrorModel({super.message, super.code, super.trace, this.dioException});

  factory ApiErrorModel.fromJson(Map<String, dynamic> json) =>
      _$ApiErrorModelFromJson(json);

  Map<String, dynamic> toJson() => _$ApiErrorModelToJson(this);

  @override
  String toString() {
    return _formatError(dioException);
  }

  /// Override message: fallback to parsed Dio message if null
  @override
  dynamic get message => super.message ?? _parsedMessage;

  /// Override code: fallback to Dio response status code
  @override
  int? get code => super.code ?? dioException?.response?.statusCode;

  /// Extract endpoint path from Dio
  String? get endpoint => dioException?.requestOptions.path;

  /// Extract request body from Dio
  dynamic get body => dioException?.requestOptions.data;

  /// Extract query parameters from Dio
  Map<String, dynamic>? get query =>
      dioException?.requestOptions.queryParameters;

  /// Extract response data from Dio
  dynamic get response => dioException?.response?.data;

  /// Internal helper to parse message from Dio response
  String? get _parsedMessage {
    final data = dioException?.response?.data;
    if (data is Map<String, dynamic>) {
      final message = data['message'];
      if (message is String) return message;
      if (message is List && message.isNotEmpty) {
        return message.first.toString();
      }
    }
    return dioException?.message;
  }
}

@JsonSerializable()
class GenericErrorModel extends BaseErrorModel {
  GenericErrorModel({super.message, super.code, super.trace});
}

/// "GET /requests → 404: Not found" — what failed, where, and why, for logs.
String _formatError(DioException? error) {
  if (error == null) return 'Error';
  final request = error.requestOptions;
  final status = error.response?.statusCode;
  return '${request.method} ${request.path}'
      '${status == null ? '' : ' → $status'}: ${error.message ?? error.type.name}';
}
