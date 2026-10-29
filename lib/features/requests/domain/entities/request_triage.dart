/// How the API classes an inbound email: a real request, or one from a sender
/// it doesn't know ("maybe"), which lapses unless the coordinator keeps it.
enum RequestTriage {
  request('request'),
  maybe('maybe');

  /// Value of the `triage` query parameter.
  final String value;

  const RequestTriage(this.value);
}
