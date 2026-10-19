/* Where the demo-request / contact form is delivered.
   It writes one document per submission to the `demoRequests` collection of
   the app's Firebase project through Firestore's public REST endpoint. Only
   `create` is allowed by firestore.rules; nothing can be read back from the
   website. No API key or credential is needed or included here. */
window.INSPECTA_CONFIG = {
  contact: {
    projectId: 'inspecta-68798',
    collection: 'demoRequests'
  }
};
