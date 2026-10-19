import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:get_it/get_it.dart';

import 'core/framework/app_cubit.dart';
import 'core/framework/app_preferences.dart';
import 'features/auth/data/datasources/auth_local_datasource.dart';
import 'features/auth/data/datasources/auth_remote_datasource.dart';
import 'features/auth/data/repositories/auth_repository_impl.dart';
import 'features/auth/domain/repositories/auth_repository.dart';
import 'features/auth/domain/usecases/get_current_user.dart';
import 'features/auth/domain/usecases/reset_password.dart';
import 'features/auth/domain/usecases/sign_in.dart';
import 'features/auth/domain/usecases/sign_out.dart';
import 'features/auth/presentation/bloc/auth_cubit.dart';
import 'features/auth/presentation/bloc/forgot_password_cubit.dart';
import 'features/auth/presentation/bloc/profile_stats_cubit.dart';
import 'features/auth/presentation/bloc/sign_in_cubit.dart';
import 'features/clients/data/datasources/clients_remote_datasource.dart';
import 'features/clients/data/repositories/clients_repository_impl.dart';
import 'features/clients/domain/repositories/clients_repository.dart';
import 'features/clients/domain/usecases/add_contact.dart';
import 'features/clients/domain/usecases/create_client.dart';
import 'features/clients/domain/usecases/get_client_detail.dart';
import 'features/clients/domain/usecases/get_clients.dart';
import 'features/clients/domain/usecases/set_certificates_email.dart';
import 'features/clients/presentation/bloc/client_detail_cubit.dart';
import 'features/clients/presentation/bloc/clients_list_cubit.dart';
import 'features/auth/domain/entities/user.dart';
import 'features/certificate/data/datasources/certificate_remote_datasource.dart';
import 'features/certificate/data/repositories/certificate_repository_impl.dart';
import 'features/certificate/domain/repositories/certificate_repository.dart';
import 'features/certificate/domain/usecases/approve_certificate.dart';
import 'features/certificate/domain/usecases/return_certificate.dart';
import 'features/certificate/domain/usecases/save_certificate_draft.dart';
import 'features/certificate/domain/usecases/submit_certificate.dart';
import 'features/certificate/domain/usecases/watch_certificate.dart';
import 'features/certificate/domain/usecases/watch_inspector_certificates.dart';
import 'features/certificate/domain/usecases/watch_reviewed_certificates.dart';
import 'features/certificate/presentation/bloc/certificate_cubit.dart';
import 'features/coordinator/data/datasources/inspectors_remote_datasource.dart';
import 'features/coordinator/data/repositories/coordinator_repository_impl.dart';
import 'features/coordinator/domain/repositories/coordinator_repository.dart';
import 'features/coordinator/domain/usecases/get_inspectors.dart';
import 'features/coordinator/domain/usecases/mark_inspector_leave.dart';
import 'features/coordinator/presentation/bloc/assign_inspector_cubit.dart';
import 'features/coordinator/presentation/bloc/coordinator_queue_cubit.dart';
import 'features/coordinator/presentation/bloc/inspector_detail_cubit.dart';
import 'features/coordinator/presentation/bloc/inspectors_list_cubit.dart';
import 'features/inspector/presentation/bloc/certificates_list_cubit.dart';
import 'features/inspector/presentation/bloc/inspector_tasks_cubit.dart';
import 'features/quotation/data/datasources/quotation_remote_datasource.dart';
import 'features/quotation/data/repositories/quotation_repository_impl.dart';
import 'features/quotation/domain/repositories/quotation_repository.dart';
import 'features/quotation/domain/usecases/get_quotations.dart';
import 'features/quotation/domain/usecases/log_client_response.dart';
import 'features/quotation/domain/usecases/save_draft.dart';
import 'features/quotation/domain/usecases/send_quotation.dart';
import 'features/quotation/domain/usecases/send_reminder.dart';
import 'features/quotation/presentation/bloc/quotation_detail_cubit.dart';
import 'features/quotation/presentation/bloc/quotation_form_cubit.dart';
import 'features/quotation/presentation/bloc/quotations_list_cubit.dart';
import 'features/requests/data/datasources/requests_remote_datasource.dart';
import 'features/requests/data/repositories/requests_repository_impl.dart';
import 'features/requests/domain/entities/inspection_request.dart';
import 'features/requests/domain/repositories/requests_repository.dart';
import 'features/requests/domain/usecases/accept_task.dart';
import 'features/requests/domain/usecases/assign_client.dart';
import 'features/requests/domain/usecases/assign_inspector.dart';
import 'features/requests/domain/usecases/complete_intake.dart';
import 'features/requests/domain/usecases/decline_task.dart';
import 'features/requests/domain/usecases/get_my_tasks.dart';
import 'features/requests/domain/usecases/get_request_detail.dart';
import 'features/requests/domain/usecases/get_requests.dart';
import 'features/requests/domain/usecases/start_inspection.dart';
import 'features/requests/presentation/bloc/requests_list_cubit.dart';
import 'features/technical_manager/data/datasources/signature_remote_datasource.dart';
import 'features/technical_manager/data/repositories/signature_repository_impl.dart';
import 'features/technical_manager/domain/repositories/signature_repository.dart';
import 'features/technical_manager/domain/usecases/save_signature.dart';
import 'features/technical_manager/domain/usecases/watch_signature.dart';
import 'features/technical_manager/presentation/bloc/my_signature_cubit.dart';
import 'features/technical_manager/presentation/bloc/review_detail_cubit.dart';
import 'features/technical_manager/presentation/bloc/review_queue_cubit.dart';
import 'features/timesheet/data/datasources/timesheet_remote_datasource.dart';
import 'features/timesheet/data/repositories/timesheet_repository_impl.dart';
import 'features/timesheet/domain/repositories/timesheet_repository.dart';
import 'features/timesheet/domain/usecases/approve_timesheet_entry.dart';
import 'features/timesheet/domain/usecases/return_timesheet_entry.dart';
import 'features/timesheet/domain/usecases/save_hourly_rate.dart';
import 'features/timesheet/domain/usecases/save_timesheet_entry.dart';
import 'features/timesheet/domain/usecases/watch_all_timesheets.dart';
import 'features/timesheet/domain/usecases/watch_hourly_rate.dart';
import 'features/timesheet/domain/usecases/watch_timesheet.dart';
import 'features/timesheet/presentation/bloc/timesheet_cubit.dart';
import 'features/timesheet/presentation/bloc/timesheet_review_cubit.dart';

final getIt = GetIt.instance;

/// Manual DI wiring. Must complete before [App] is built, since [AppCubit]
/// reads [AppPreferences] synchronously in its constructor.
Future<void> setupDependencies() async {
  const storage = FlutterSecureStorage();
  final preferences = AppPreferences(storage);
  await preferences.init();

  getIt.registerSingleton<AppPreferences>(preferences);
  getIt.registerSingleton<AppCubit>(AppCubit(preferences));

  _registerAuth(storage);
  _registerRequests();
  _registerQuotation();
  _registerClients();
  _registerCoordinator();
  _registerProfile();
  _registerInspector();
  _registerCertificate();
  _registerTechnicalManager();
  _registerTimesheet();
}

void _registerAuth(FlutterSecureStorage storage) {
  getIt
    ..registerLazySingleton(AuthRemoteDataSource.new)
    ..registerLazySingleton(() => AuthLocalDataSource(storage))
    ..registerLazySingleton<AuthRepository>(
      () => AuthRepositoryImpl(getIt(), getIt()),
    )
    ..registerLazySingleton(() => SignIn(getIt()))
    ..registerLazySingleton(() => SignOut(getIt()))
    ..registerLazySingleton(() => GetCurrentUser(getIt()))
    ..registerLazySingleton(() => ResetPassword(getIt()))
    ..registerLazySingleton(() => AuthCubit(getIt(), getIt()))
    ..registerFactory(() => SignInCubit(getIt()))
    ..registerFactory(() => ForgotPasswordCubit(getIt()));
}

void _registerRequests() {
  getIt
    ..registerLazySingleton(RequestsRemoteDataSource.new)
    ..registerLazySingleton<RequestsRepository>(
      () => RequestsRepositoryImpl(getIt()),
    )
    ..registerLazySingleton(() => GetRequests(getIt()))
    ..registerLazySingleton(() => GetRequestDetail(getIt()))
    ..registerLazySingleton(() => AssignClient(getIt()))
    ..registerLazySingleton(() => AssignInspector(getIt()))
    ..registerLazySingleton(() => CompleteIntake(getIt()))
    ..registerLazySingleton(() => GetMyTasks(getIt()))
    ..registerLazySingleton(() => AcceptTask(getIt()))
    ..registerLazySingleton(() => DeclineTask(getIt()))
    ..registerLazySingleton(() => StartInspection(getIt()))
    ..registerFactory(() => RequestsListCubit(getIt()));
}

void _registerQuotation() {
  getIt
    ..registerLazySingleton(QuotationRemoteDataSource.new)
    ..registerLazySingleton<QuotationRepository>(
      () => QuotationRepositoryImpl(getIt()),
    )
    ..registerLazySingleton(() => GetQuotations(getIt()))
    ..registerLazySingleton(() => SaveDraft(getIt()))
    ..registerLazySingleton(() => SendQuotation(getIt(), getIt()))
    ..registerLazySingleton(() => LogClientResponse(getIt(), getIt()))
    ..registerLazySingleton(() => SendReminder(getIt()))
    ..registerFactory(() => QuotationsListCubit(getIt()))
    ..registerFactoryParam<QuotationFormCubit, InspectionRequest, void>(
      (request, _) => QuotationFormCubit(
        request: request,
        saveDraft: getIt(),
        sendQuotation: getIt(),
      ),
    )
    ..registerFactoryParam<QuotationDetailCubit, String, void>(
      (requestId, _) => QuotationDetailCubit(
        requestId: requestId,
        repository: getIt(),
        logClientResponse: getIt(),
        getRequestDetail: getIt(),
      ),
    );
}

void _registerClients() {
  getIt
    ..registerLazySingleton(ClientsRemoteDataSource.new)
    ..registerLazySingleton<ClientsRepository>(
      () => ClientsRepositoryImpl(getIt()),
    )
    ..registerLazySingleton(() => GetClients(getIt()))
    ..registerLazySingleton(() => GetClientDetail(getIt()))
    ..registerLazySingleton(() => CreateClient(getIt()))
    ..registerLazySingleton(() => AddContact(getIt()))
    ..registerLazySingleton(() => SetCertificatesEmail(getIt()))
    ..registerFactory(() => ClientsListCubit(getIt(), getIt()))
    ..registerFactoryParam<ClientDetailCubit, String, void>(
      (clientId, _) => ClientDetailCubit(
        clientId: clientId,
        getClientDetail: getIt(),
        getRequests: getIt(),
      ),
    );
}

void _registerCoordinator() {
  getIt
    ..registerLazySingleton(InspectorsRemoteDataSource.new)
    ..registerLazySingleton<CoordinatorRepository>(
      () => CoordinatorRepositoryImpl(getIt()),
    )
    ..registerLazySingleton(() => GetInspectors(getIt()))
    ..registerFactory(() => CoordinatorQueueCubit(getIt(), getIt()))
    ..registerFactoryParam<AssignInspectorCubit, InspectionRequest, String?>(
      (request, preselectedInspectorId) => AssignInspectorCubit(
        request: request,
        preselectedInspectorId: preselectedInspectorId,
        getInspectors: getIt(),
        assignInspector: getIt(),
      ),
    )
    ..registerFactory(() => InspectorsListCubit(getIt(), getIt()))
    ..registerLazySingleton(() => MarkInspectorLeave(getIt()))
    ..registerFactoryParam<InspectorDetailCubit, AppUser, void>(
      (inspector, _) => InspectorDetailCubit(
        inspector: inspector,
        getRequests: getIt(),
        markInspectorLeave: getIt(),
      ),
    );
}

void _registerProfile() {
  getIt.registerFactory(() => ProfileStatsCubit(getIt(), getIt(), getIt(), getIt(), getIt(), getIt()));
}

void _registerInspector() {
  getIt
    ..registerFactory(
      () => InspectorTasksCubit(
        inspectorId: getIt<AuthCubit>().user!.id,
        getMyTasks: getIt(),
        acceptTask: getIt(),
        declineTask: getIt(),
        startInspection: getIt(),
      ),
    )
    ..registerFactory(
      () => CertificatesListCubit(inspectorId: getIt<AuthCubit>().user!.id, getMyTasks: getIt()),
    );
}

void _registerCertificate() {
  getIt
    ..registerLazySingleton(CertificateRemoteDataSource.new)
    ..registerLazySingleton<CertificateRepository>(
      () => CertificateRepositoryImpl(getIt(), getIt()),
    )
    ..registerLazySingleton(() => WatchCertificate(getIt()))
    ..registerLazySingleton(() => WatchReviewedCertificates(getIt()))
    ..registerLazySingleton(() => WatchInspectorCertificates(getIt()))
    ..registerLazySingleton(() => SaveCertificateDraft(getIt()))
    ..registerLazySingleton(() => SubmitCertificate(getIt()))
    ..registerLazySingleton(() => ApproveCertificate(getIt()))
    ..registerLazySingleton(() => ReturnCertificate(getIt()))
    ..registerFactoryParam<CertificateCubit, InspectionRequest, void>(
      (request, _) => CertificateCubit(
        request: request,
        inspector: getIt<AuthCubit>().user,
        watchCertificate: getIt(),
        saveDraft: getIt(),
        submitCertificate: getIt(),
      ),
    );
}

void _registerTechnicalManager() {
  getIt
    ..registerLazySingleton(SignatureRemoteDataSource.new)
    ..registerLazySingleton<SignatureRepository>(() => SignatureRepositoryImpl(getIt()))
    ..registerLazySingleton(() => WatchSignature(getIt()))
    ..registerLazySingleton(() => SaveSignature(getIt()))
    ..registerFactory(() => MySignatureCubit(getIt(), getIt()))
    ..registerFactory(() => ReviewQueueCubit(getIt(), getIt(), getIt()))
    ..registerFactoryParam<ReviewDetailCubit, InspectionRequest, String>(
      (request, reviewerName) => ReviewDetailCubit(
        request: request,
        reviewerName: reviewerName,
        managerId: getIt<AuthCubit>().user?.id ?? '',
        watchCertificate: getIt(),
        getClientDetail: getIt(),
        approveCertificate: getIt(),
        returnCertificate: getIt(),
        watchSignature: getIt(),
        saveSignature: getIt(),
      ),
    );
}

void _registerTimesheet() {
  getIt
    ..registerLazySingleton(TimesheetRemoteDataSource.new)
    ..registerLazySingleton<TimesheetRepository>(() => TimesheetRepositoryImpl(getIt()))
    ..registerLazySingleton(() => WatchTimesheet(getIt()))
    ..registerLazySingleton(() => SaveTimesheetEntry(getIt()))
    ..registerLazySingleton(() => WatchHourlyRate(getIt()))
    ..registerLazySingleton(() => SaveHourlyRate(getIt()))
    ..registerLazySingleton(() => WatchAllTimesheets(getIt()))
    ..registerLazySingleton(() => ApproveTimesheetEntry(getIt()))
    ..registerLazySingleton(() => ReturnTimesheetEntry(getIt()))
    ..registerFactory(
      () => TimesheetCubit(
        inspectorId: getIt<AuthCubit>().user!.id,
        getMyTasks: getIt(),
        watchTimesheet: getIt(),
        watchHourlyRate: getIt(),
        saveEntry: getIt(),
        saveHourlyRate: getIt(),
      ),
    )
    ..registerFactory(
      () => TimesheetReviewCubit(
        reviewerId: getIt<AuthCubit>().user!.id,
        reviewerName: getIt<AuthCubit>().user!.name,
        watchAllTimesheets: getIt(),
        getRequests: getIt(),
        getInspectors: getIt(),
        approveEntry: getIt(),
        returnEntry: getIt(),
      ),
    );
}
