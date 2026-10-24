import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:inspecta/core/error/api_error_model.dart';
part 'api_result.freezed.dart';

@freezed
class ApiResult<T> with _$ApiResult<T> {
  const factory ApiResult.success(T data) = Success<T>;

  const factory ApiResult.failure(BaseErrorModel apiErrorModel) = Failure<T>;
}

extension ApiResultX<T> on ApiResult<T> {
  T? get successOrNull => this is Success<T> ? (this as Success<T>).data : null;
}
