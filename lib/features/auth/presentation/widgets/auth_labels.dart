import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/auth_failure.dart';
import '../../domain/entities/user_role.dart';

extension AuthFailureMessage on AuthFailureCode {
  String message(AppLocalizations t) => switch (this) {
    AuthFailureCode.invalidEmail => t.errorEmailInvalid,
    AuthFailureCode.invalidCredentials => t.errorInvalidCredentials,
    AuthFailureCode.accountDisabled => t.errorAccountDisabled,
    AuthFailureCode.noProfile => t.errorNoProfile,
    AuthFailureCode.tooManyAttempts => t.errorTooManyAttempts,
    AuthFailureCode.network => t.errorNetwork,
    AuthFailureCode.unavailable => t.errorUnavailable,
    AuthFailureCode.unknown => t.errorUnknown,
  };
}

extension UserRoleLabels on UserRole {
  String label(AppLocalizations t) => switch (this) {
    UserRole.coordinator => t.roleCoordinator,
    UserRole.inspector => t.roleInspector,
    UserRole.technicalManager => t.roleTechnicalManager,
    UserRole.admin => t.roleAdmin,
    UserRole.finance => t.roleFinance,
    UserRole.managingDirector => t.roleManagingDirector,
    UserRole.merchandiser => t.roleMerchandiser,
  };

  /// Title of the role's home screen (BR-01.2).
  String homeTitle(AppLocalizations t) => switch (this) {
    UserRole.coordinator => t.homeCoordinator,
    UserRole.inspector => t.homeInspector,
    UserRole.technicalManager => t.homeTechnicalManager,
    UserRole.admin => t.homeAdmin,
    UserRole.finance ||
    UserRole.managingDirector ||
    UserRole.merchandiser => t.homeWms,
  };
}
