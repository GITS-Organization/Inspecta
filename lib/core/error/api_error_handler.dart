import 'package:dio/dio.dart';

import 'api_error_model.dart';

class ApiErrorHandler {
  static BaseErrorModel handle(dynamic error, {StackTrace? trace}) {
    if (error is DioException) {
      switch (error.type) {
        case DioExceptionType.connectionError:
          return ApiErrorModel(
            message: "Connection to server failed",
            dioException: error,
            trace: trace,
          );
        case DioExceptionType.cancel:
          return ApiErrorModel(
            message: "Request to the server was cancelled",
            dioException: error,
            trace: trace,
          );
        case DioExceptionType.connectionTimeout:
          return ApiErrorModel(
            message: "Connection timeout with the server",
            dioException: error,
            trace: trace,
          );
        case DioExceptionType.unknown:
          return ApiErrorModel(
            message:
                "Connection to the server failed due to internet connection",
            dioException: error,
            trace: trace,
          );
        case DioExceptionType.receiveTimeout:
          return ApiErrorModel(
            message: "Receive timeout in connection with the server",
            dioException: error,
            trace: trace,
          );
        case DioExceptionType.sendTimeout:
          return ApiErrorModel(
            message: "Send timeout in connection with the server",
            dioException: error,
            trace: trace,
          );
        case DioExceptionType.badResponse:
          return _handleError(error, trace: trace);
        default:
          return ApiErrorModel(
            message: "Something went wrong",
            dioException: error,
            trace: trace,
          );
      }
    } else {
      return GenericErrorModel(
        message: "Unknown error occurred $error",
        trace: trace,
      );
    }
  }
}

ApiErrorModel _handleError(DioException error, {StackTrace? trace}) {
  final data = error.response?.data;
  if (data is! Map) {
    return ApiErrorModel(
      message: "Unexpected error occurred",
      code: error.response?.statusCode,
      dioException: error,
      trace: trace,
    );
  }

  String? key;

  if (data['message'] is String) {
    key = data['message'] as String?;
  } else if (data['message'] is List && (data['message'] as List).isNotEmpty) {
    final messageList = data['message'] as List;
    key = messageList.first?.toString();
  } else {
    key = data['message']?.toString();
  }

  final int? statusCode = data['statusCode'];

  final message = key?.toReadableMessage() ?? "Unexpected error occurred";

  return ApiErrorModel(
    message: message,
    code: statusCode,
    dioException: error,
    trace: trace,
  );
}

extension MessageFormatter on String {
  String toReadableMessage() {
    return toLowerCase()
        .split('_')
        .map(
          (word) =>
              word.isNotEmpty ? word[0].toUpperCase() + word.substring(1) : '',
        ).join(' ');
  }
}
