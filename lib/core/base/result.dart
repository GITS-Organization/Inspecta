
import '../error/failure.dart';

/// The outcome of a request: its data, or why it failed.
sealed class Result<T> {
  const Result();

  R fold<R>(
    R Function(T data) onSuccess,
    R Function(Failure failure) onError,
  ) => switch (this) {
    Success(:final data) => onSuccess(data),
    Err(:final failure) => onError(failure),
  };
}

class Success<T> extends Result<T> {
  final T data;

  const Success(this.data);
}

class Err<T> extends Result<T> {
  final Failure failure;

  const Err(this.failure);
}
