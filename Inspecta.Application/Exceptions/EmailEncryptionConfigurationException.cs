namespace Inspecta.Application.Exceptions;

public sealed class EmailEncryptionConfigurationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
