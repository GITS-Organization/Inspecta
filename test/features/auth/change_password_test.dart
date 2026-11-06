import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/builder/flow_state.dart';
import 'package:inspecta/core/utils/validators.dart';
import 'package:inspecta/features/auth/data/datasources/auth_api_client_services.dart';
import 'package:inspecta/features/auth/data/datasources/auth_local_datasource.dart';
import 'package:inspecta/features/auth/data/models/auth_requests.dart';
import 'package:inspecta/features/auth/data/models/auth_session_model.dart';
import 'package:inspecta/features/auth/data/repositories/auth_repository_impl.dart';
import 'package:inspecta/features/auth/domain/entities/auth_failure.dart';
import 'package:inspecta/features/auth/domain/repositories/auth_repository.dart';
import 'package:inspecta/features/auth/domain/usecases/change_password.dart';
import 'package:inspecta/features/auth/presentation/bloc/change_password_cubit.dart';

class _Api extends Fake implements AuthApiClientServices {
  ChangePasswordRequest? sent;
  Object? failure;

  @override
  Future<void> changeMyPassword(ChangePasswordRequest request) async {
    sent = request;
    if (failure != null) throw failure!;
  }
}

class _Local extends Fake implements AuthLocalDataSource {
  final AuthSessionModel? session;

  _Local(this.session);

  @override
  Future<AuthSessionModel?> readSession() async => session;
}

AuthSessionModel _session() => AuthSessionModel(
  accessToken: 'access',
  refreshToken: 'this-device-refresh-token',
  expiresAt: DateTime(2100),
  user: const AuthUserModel(
    id: 'u1',
    email: 'user@example.com',
    fullName: 'User',
    role: 'coordinator',
    isActive: true,
  ),
);

/// What the API sends back for a refused request: a status and a body.
DioException _refused(int status, [Object? body]) {
  final options = RequestOptions(path: 'auth/change-my-password');
  return DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response(requestOptions: options, statusCode: status, data: body),
  );
}

Future<AuthFailureCode?> _codeOf(Future<void> Function() action) async {
  try {
    await action();
    return null;
  } on AuthFailure catch (f) {
    return f.code;
  }
}

class _Repo extends Fake implements AuthRepository {
  Object? failure;
  int calls = 0;

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    calls++;
    if (failure != null) throw failure!;
  }
}

void main() {
  group('the password rules', () {
    test('match the API: 12+ characters, both cases, a digit, a symbol', () {
      expect(Validators.isStrongPassword('Brand-New-Passw0rd!'), isTrue);
      expect(Validators.isStrongPassword('Sh0rt!aA'), isFalse);
      expect(Validators.isStrongPassword('alllowercase-passw0rd!'), isFalse);
      expect(Validators.isStrongPassword('ALLUPPERCASE-PASSW0RD!'), isFalse);
      expect(Validators.isStrongPassword('No-Digits-In-This-One!'), isFalse);
      expect(Validators.isStrongPassword('NoSymbolsInThis0ne12'), isFalse);
    });
  });

  group('changing the password through the repository', () {
    test('sends both passwords and this device\'s refresh token', () async {
      final api = _Api();
      final repository = AuthRepositoryImpl(api, _Local(_session()));

      await repository.changePassword(
        currentPassword: 'Correct-Horse-Battery-1',
        newPassword: 'Brand-New-Passw0rd!',
      );

      expect(api.sent!.toJson(), {
        'currentPassword': 'Correct-Horse-Battery-1',
        'newPassword': 'Brand-New-Passw0rd!',
        'refreshToken': 'this-device-refresh-token',
      });
    });

    test('leaves the refresh token out when there is no stored session', () async {
      final api = _Api();

      await AuthRepositoryImpl(api, _Local(null)).changePassword(
        currentPassword: 'a',
        newPassword: 'b',
      );

      expect(api.sent!.toJson().containsKey('refreshToken'), isFalse);
    });

    test('reads the API\'s reason from the body of a 400', () async {
      Future<AuthFailureCode?> reason(String code) {
        final api = _Api()..failure = _refused(400, {'code': code});
        return _codeOf(
          () => AuthRepositoryImpl(api, _Local(_session())).changePassword(
            currentPassword: 'a',
            newPassword: 'b',
          ),
        );
      }

      expect(
        await reason('current_password_incorrect'),
        AuthFailureCode.currentPasswordIncorrect,
      );
      expect(await reason('password_policy_failed'), AuthFailureCode.weakPassword);
      expect(await reason('password_unchanged'), AuthFailureCode.passwordUnchanged);
    });

    test('too many guesses is its own failure', () async {
      final api = _Api()..failure = _refused(429, {'code': 'too_many_attempts'});

      final code = await _codeOf(
        () => AuthRepositoryImpl(api, _Local(_session())).changePassword(
          currentPassword: 'a',
          newPassword: 'b',
        ),
      );

      expect(code, AuthFailureCode.tooManyAttempts);
    });

    test('no connection is a network failure', () async {
      final api = _Api()
        ..failure = DioException(
          requestOptions: RequestOptions(path: 'x'),
          type: DioExceptionType.connectionError,
        );

      final code = await _codeOf(
        () => AuthRepositoryImpl(api, _Local(_session())).changePassword(
          currentPassword: 'a',
          newPassword: 'b',
        ),
      );

      expect(code, AuthFailureCode.network);
    });
  });

  group('the change password screen state', () {
    test('saving ends in success', () async {
      final repo = _Repo();
      final cubit = ChangePasswordCubit(ChangePassword(repo));

      await cubit.submit(currentPassword: 'a', newPassword: 'b');

      expect(cubit.state.type, StateType.success);
      expect(cubit.isSaved, isTrue);
      await cubit.close();
    });

    test('a refusal ends in an error carrying the reason', () async {
      final repo = _Repo()
        ..failure = const AuthFailure(AuthFailureCode.currentPasswordIncorrect);
      final cubit = ChangePasswordCubit(ChangePassword(repo));

      await cubit.submit(currentPassword: 'a', newPassword: 'b');

      expect(cubit.state.type, StateType.error);
      expect(cubit.failure, AuthFailureCode.currentPasswordIncorrect);
      await cubit.close();
    });
  });
}
