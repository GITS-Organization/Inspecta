import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:get_it/get_it.dart';

import 'core/framework/app_cubit.dart';
import 'core/framework/app_preferences.dart';
import 'core/networking/dio_factory.dart';
import 'features/auth/data/datasources/auth_api_client_services.dart';
import 'features/auth/data/datasources/auth_local_datasource.dart';
import 'features/auth/data/datasources/auth_remote_datasource.dart';
import 'features/auth/data/datasources/signature_local_datasource.dart';
import 'features/auth/data/repositories/auth_repository_impl.dart';
import 'features/auth/domain/repositories/auth_repository.dart';
import 'features/auth/domain/usecases/get_current_user.dart';
import 'features/auth/domain/usecases/reset_password.dart';
import 'features/auth/domain/usecases/sign_in.dart';
import 'features/auth/domain/usecases/refresh_session.dart';
import 'features/auth/domain/usecases/sign_out.dart';
import 'features/auth/presentation/bloc/auth_cubit.dart';
import 'features/auth/presentation/bloc/forgot_password_cubit.dart';
import 'features/auth/presentation/bloc/my_signature_cubit.dart';
import 'features/auth/presentation/bloc/profile_stats_cubit.dart';
import 'features/auth/presentation/bloc/sign_in_cubit.dart';
import 'features/certificate/data/datasources/certificate_remote_datasource.dart';
import 'features/certificate/data/repositories/certificate_repository_impl.dart';
import 'features/certificate/domain/repositories/certificate_repository.dart';
import 'features/certificate/domain/machine_certificate_source.dart';
import 'features/certificate/domain/usecases/send_certificate_to_client.dart';
import 'features/certificate/domain/usecases/watch_certificate.dart';
import 'features/certificate/presentation/bloc/certificate_cubit.dart';
import 'features/coordinator/data/datasources/coordinator_api_client_services.dart';
import 'features/coordinator/data/datasources/inspectors_remote_datasource.dart';
import 'features/coordinator/data/repositories/coordinator_repository_impl.dart';
import 'features/coordinator/domain/repositories/coordinator_repository.dart';
import 'features/coordinator/domain/usecases/get_inspectors.dart';
import 'features/coordinator/domain/usecases/list_inspectors.dart';
import 'features/coordinator/presentation/bloc/assign_inspector_cubit.dart';
import 'features/machines/data/datasources/machines_api_client_services.dart';
import 'features/machines/data/repositories/machines_repository_impl.dart';
import 'features/machines/domain/repositories/machines_repository.dart';
import 'features/machines/domain/usecases/approve_all_machines.dart';
import 'features/machines/domain/usecases/deliver_to_client.dart';
import 'features/machines/domain/usecases/get_delivery_preview.dart';
import 'features/machines/domain/usecases/approve_machine.dart';
import 'features/machines/domain/usecases/return_job_to_inspector.dart';
import 'features/machines/domain/usecases/get_job_machines.dart';
import 'features/machines/domain/usecases/get_machine_form.dart';
import 'features/machines/domain/usecases/get_machine_pdf.dart';
import 'features/machines/domain/usecases/return_machine.dart';
import 'features/machines/domain/usecases/save_machine_form.dart';
import 'features/machines/domain/usecases/submit_machine.dart';
import 'features/machines/domain/usecases/start_job_inspection.dart';
import 'features/machines/presentation/bloc/job_machines_cubit.dart';
import 'features/queue/data/datasources/queue_api_client_services.dart';
import 'features/queue/data/repositories/queue_repository_impl.dart';
import 'features/queue/domain/repositories/queue_repository.dart';
import 'features/queue/domain/usecases/get_queue.dart';
import 'features/queue/domain/usecases/get_to_review.dart';
import 'features/queue/presentation/bloc/coordinator_queue_cubit.dart';
import 'features/coordinator/presentation/bloc/send_certificate_cubit.dart';
import 'features/inspector/presentation/bloc/certificates_list_cubit.dart';
import 'features/inspector/presentation/bloc/inspector_tasks_cubit.dart';
import 'features/quotation/data/datasources/quotation_api_client_services.dart';
import 'features/quotation/data/datasources/quotation_remote_datasource.dart';
import 'features/quotation/data/repositories/quotation_repository_impl.dart';
import 'features/quotation/domain/entities/quotation.dart';
import 'features/quotation/domain/repositories/quotation_repository.dart';
import 'features/quotation/domain/usecases/get_quotation_board.dart';
import 'features/quotation/domain/usecases/get_quote_pdf.dart';
import 'features/quotation/domain/usecases/get_quotations.dart';
import 'features/quotation/domain/usecases/update_offer.dart';
import 'features/quotation/domain/usecases/log_client_response.dart';
import 'features/quotation/domain/usecases/save_draft.dart';
import 'features/quotation/domain/usecases/send_new_price.dart';
import 'features/quotation/domain/usecases/send_quotation.dart';
import 'features/quotation/presentation/bloc/quotation_detail_cubit.dart';
import 'features/quotation/presentation/bloc/quotation_form_cubit.dart';
import 'features/quotation/presentation/bloc/quotations_list_cubit.dart';
import 'features/requests/data/datasources/requests_api_client_services.dart';
import 'features/requests/data/datasources/requests_remote_datasource.dart';
import 'features/requests/data/repositories/requests_repository_impl.dart';
import 'features/requests/domain/entities/inspection_request.dart';
import 'features/requests/domain/repositories/requests_repository.dart';
import 'features/requests/domain/usecases/assign_inspector.dart';
import 'features/requests/domain/usecases/complete_intake.dart';
import 'features/requests/domain/usecases/confirm_request.dart';
import 'features/requests/domain/usecases/delete_request.dart';
import 'features/requests/domain/usecases/get_triaged_requests.dart';
import 'features/requests/domain/usecases/get_request_detail.dart';
import 'features/requests/domain/usecases/get_requests.dart';
import 'features/requests/presentation/bloc/requests_list_cubit.dart';
import 'features/technical_manager/presentation/bloc/review_detail_cubit.dart';
import 'features/technical_manager/presentation/bloc/review_queue_cubit.dart';
import 'features/technical_manager/presentation/bloc/review_summary_cubit.dart';

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
  _registerCoordinator();
  _registerQueue();
  _registerMachines();
  _registerProfile(storage);
  _registerInspector();
  _registerCertificate();
  _registerTechnicalManager();
}

void _registerAuth(FlutterSecureStorage storage) {
  getIt
    ..registerLazySingleton(() => AuthApiClientServices(DioFactory.getDio()))
    ..registerLazySingleton(AuthRemoteDataSource.new)
    ..registerLazySingleton(() => AuthLocalDataSource(storage))
    ..registerLazySingleton<AuthRepository>(
      () => AuthRepositoryImpl(getIt(), getIt(), getIt()),
    )
    ..registerLazySingleton(() => SignIn(getIt()))
    ..registerLazySingleton(() => SignOut(getIt()))
    ..registerLazySingleton(() => RefreshSession(getIt()))
    ..registerLazySingleton(() => GetCurrentUser(getIt()))
    ..registerLazySingleton(() => ResetPassword(getIt()))
    ..registerLazySingleton(() => AuthCubit(getIt(), getIt()))
    ..registerFactory(() => SignInCubit(getIt()))
    ..registerFactory(() => ForgotPasswordCubit(getIt()));
}

void _registerRequests() {
  getIt
    ..registerLazySingleton(
      () => RequestsRemoteDataSource(currentUserId: () => getIt<AuthCubit>().user?.id),
    )
    ..registerLazySingleton(() => RequestsApiClientServices(DioFactory.getDio()))
    ..registerLazySingleton<RequestsRepository>(
      () => RequestsRepositoryImpl(getIt(), getIt()),
    )
    ..registerLazySingleton(() => GetRequests(getIt()))
    ..registerLazySingleton(() => GetTriagedRequests(getIt()))
    ..registerLazySingleton(() => ConfirmRequest(getIt()))
    ..registerLazySingleton(() => DeleteRequest(getIt()))
    ..registerLazySingleton(() => GetRequestDetail(getIt()))
    ..registerLazySingleton(() => AssignInspector(getIt()))
    ..registerLazySingleton(() => CompleteIntake(getIt()))
    ..registerFactory(() => RequestsListCubit(getIt(), getIt(), getIt()));
}

void _registerQuotation() {
  getIt
    ..registerLazySingleton(
      () => QuotationRemoteDataSource(currentUserId: () => getIt<AuthCubit>().user?.id),
    )
    ..registerLazySingleton(() => QuotationApiClientServices(DioFactory.getDio()))
    ..registerLazySingleton<QuotationRepository>(
      () => QuotationRepositoryImpl(getIt(), getIt()),
    )
    ..registerLazySingleton(() => GetQuotations(getIt()))
    ..registerLazySingleton(() => SaveDraft(getIt()))
    ..registerLazySingleton(() => SendQuotation(getIt(), getIt()))
    ..registerLazySingleton(() => LogClientResponse(getIt(), getIt()))
    ..registerLazySingleton(() => SendNewPrice(getIt()))
    ..registerLazySingleton(() => GetQuotationBoard(getIt()))
    ..registerFactory(() => QuotationsListCubit(getIt()))
    ..registerLazySingleton(() => UpdateOffer(getIt()))
    ..registerLazySingleton(() => GetQuotePdf(getIt()))
    ..registerFactoryParam<QuotationFormCubit, InspectionRequest, String?>(
      (request, quotationId) => QuotationFormCubit(
        request: request,
        quotationId: quotationId,
        saveDraft: getIt(),
        sendQuotation: getIt(),
        updateOffer: getIt(),
        getQuotePdf: getIt(),
        getRequestDetail: getIt(),
      ),
    )
    ..registerFactoryParam<
      QuotationDetailCubit,
      String,
      ({Quotation? quotation, InspectionRequest? request})
    >(
      (requestId, seed) => QuotationDetailCubit(
        requestId: requestId,
        quotation: seed.quotation,
        request: seed.request,
        logClientResponse: getIt(),
        sendNewPrice: getIt(),
      ),
    );
}

void _registerMachines() {
  getIt
    ..registerLazySingleton(() => MachinesApiClientServices(DioFactory.getDio()))
    ..registerLazySingleton<MachinesRepository>(() => MachinesRepositoryImpl(getIt()))
    ..registerLazySingleton(() => GetJobMachines(getIt()))
    ..registerLazySingleton(() => GetMachineForm(getIt()))
    ..registerLazySingleton(() => GetMachinePdf(getIt()))
    ..registerLazySingleton(() => StartJobInspection(getIt()))
    ..registerLazySingleton(() => SaveMachineForm(getIt()))
    ..registerLazySingleton(() => SubmitMachine(getIt()))
    ..registerLazySingleton(() => ApproveMachine(getIt()))
    ..registerLazySingleton(() => ReturnMachine(getIt()))
    ..registerLazySingleton(() => GetDeliveryPreview(getIt()))
    ..registerLazySingleton(() => DeliverToClient(getIt()))
    ..registerLazySingleton(() => ApproveAllMachines(getIt()))
    ..registerLazySingleton(() => ReturnJobToInspector(getIt()))
    ..registerFactoryParam<JobMachinesCubit, String, void>(
      (requestId, _) => JobMachinesCubit(
        requestId: requestId,
        getMachines: getIt(),
        startInspection: getIt(),
      ),
    );
}

void _registerQueue() {
  getIt
    ..registerLazySingleton(() => QueueApiClientServices(DioFactory.getDio()))
    ..registerLazySingleton<QueueRepository>(() => QueueRepositoryImpl(getIt()))
    ..registerLazySingleton(() => GetQueue(getIt()))
    ..registerLazySingleton(() => GetToReview(getIt()))
    ..registerFactory(() => CoordinatorQueueCubit(getIt()));
}

void _registerCoordinator() {
  getIt
    ..registerLazySingleton(InspectorsRemoteDataSource.new)
    ..registerLazySingleton(() => CoordinatorApiClientServices(DioFactory.getDio()))
    ..registerLazySingleton<CoordinatorRepository>(
      () => CoordinatorRepositoryImpl(getIt(), getIt()),
    )
    ..registerLazySingleton(() => GetInspectors(getIt()))
    ..registerLazySingleton(() => ListInspectors(getIt()))
    ..registerFactoryParam<SendCertificateCubit, InspectionRequest, void>(
      (request, _) => SendCertificateCubit(
        request: request,
        getJobMachines: getIt(),
        getDeliveryPreview: getIt(),
        deliverToClient: getIt(),
      ),
    )
    ..registerFactoryParam<AssignInspectorCubit, InspectionRequest, String?>(
      (request, preselectedInspectorId) => AssignInspectorCubit(
        request: request,
        preselectedInspectorId: preselectedInspectorId,
        listInspectors: getIt(),
        assignInspector: getIt(),
      ),
    );
}

void _registerProfile(FlutterSecureStorage storage) {
  getIt
    ..registerLazySingleton(() => SignatureLocalDataSource(storage))
    ..registerFactory(() => ProfileStatsCubit(getIt(), getIt()))
    ..registerFactoryParam<MySignatureCubit, String, void>(
      (userId, _) => MySignatureCubit(getIt(), userId),
    );
}

void _registerInspector() {
  getIt
    ..registerFactory(
      () => InspectorTasksCubit(
        inspectorId: getIt<AuthCubit>().user!.id,
        getQueue: getIt(),
      ),
    )
    ..registerFactory(
      () => CertificatesListCubit(inspectorId: getIt<AuthCubit>().user!.id, getQueue: getIt()),
    );
}

void _registerCertificate() {
  getIt
    ..registerLazySingleton(CertificateRemoteDataSource.new)
    ..registerLazySingleton<CertificateRepository>(
      () => CertificateRepositoryImpl(getIt(), getIt()),
    )
    ..registerLazySingleton(() => WatchCertificate(getIt()))
    ..registerLazySingleton(() => SendCertificateToClient(getIt()))
    ..registerFactoryParam<CertificateCubit, InspectionRequest, String>(
      (request, requestItemId) => CertificateCubit(
        request: request,
        machine: MachineCertificateSource(
          requestItemId: requestItemId,
          getForm: getIt(),
          saveForm: getIt(),
          submit: getIt(),
        ),
      ),
    );
}

void _registerTechnicalManager() {
  getIt
    ..registerFactory(() => ReviewQueueCubit(getIt(), getIt()))
    ..registerFactoryParam<ReviewSummaryCubit, InspectionRequest, void>(
      (request, _) => ReviewSummaryCubit(
        request: request,
        userId: getIt<AuthCubit>().user?.id ?? '',
        getToReview: getIt(),
        getJobMachines: getIt(),
        approveAllMachines: getIt(),
        returnJobToInspector: getIt(),
        signatures: getIt(),
      ),
    )
    ..registerFactoryParam<ReviewDetailCubit, InspectionRequest, String>(
      (request, requestItemId) => ReviewDetailCubit(
        request: request,
        requestItemId: requestItemId,
        reviewerName: getIt<AuthCubit>().user?.name ?? '',
        getMachineForm: getIt(),
        approveMachine: getIt(),
        returnMachine: getIt(),
      ),
    );
}
