// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'api_error_model.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ApiErrorModel _$ApiErrorModelFromJson(Map<String, dynamic> json) =>
    ApiErrorModel(
      message: json['message'],
      code: (json['code'] as num?)?.toInt(),
    );

Map<String, dynamic> _$ApiErrorModelToJson(ApiErrorModel instance) =>
    <String, dynamic>{'message': instance.message, 'code': instance.code};

GenericErrorModel _$GenericErrorModelFromJson(Map<String, dynamic> json) =>
    GenericErrorModel(
      message: json['message'],
      code: (json['code'] as num?)?.toInt(),
    );

Map<String, dynamic> _$GenericErrorModelToJson(GenericErrorModel instance) =>
    <String, dynamic>{'message': instance.message, 'code': instance.code};
