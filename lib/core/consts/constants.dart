// ignore_for_file: non_constant_identifier_names, constant_identifier_names

class Constants {
  /// Identity microservice base URL (auth: login, otp, password...).
  static const String baseUrl = 'https://tvoy-identity.kabret.info';

  /// Notification microservice base URL (in-app notifications).
  static const String notificationUrl = 'https://tvoy-notification.kabret.info';

  /// flutter_secure_storage key for the refresh token (used to silently renew
  /// the access token when a request comes back 401).
  static const String refreshToken = 'refresh_token';

  /// Language code checked against the persisted language to know if Arabic
  /// is the active locale.
  static const String languageCached = 'ar';
}

class Endpoints {
  static const String login = '/api/v1/auth/login';
}
