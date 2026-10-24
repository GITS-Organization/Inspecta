import 'package:json_annotation/json_annotation.dart';

part 'base_response.g.dart';

@JsonSerializable(genericArgumentFactories: true, createToJson: false)
class BaseResponse<T> {
  final T? data;
  @JsonKey(name: 'documents')
  final T? documents;
  @JsonKey(name: 'message', defaultValue: '')
  String message;
  @JsonKey(name: 'count', defaultValue: 0)
  int count;
  @JsonKey(name: 'statusCode', defaultValue: 0)
  int statusCode;
  @JsonKey(name: 'success', defaultValue: false)
  bool success;

  BaseResponse({
    this.data,
    this.documents,
    required this.message,
    required this.statusCode,
    required this.count,
    required this.success,
  });

  factory BaseResponse.fromJson(
    Map<String, dynamic> json,
    T Function(Object? json) fromJsonT,
  ) => _$BaseResponseFromJson(json, fromJsonT);
}
