class Validators {
  Validators._();

  static final _email = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');

  /// A loose shape check; the server decides whether the address exists.
  static bool isEmail(String value) => _email.hasMatch(value.trim());
}
