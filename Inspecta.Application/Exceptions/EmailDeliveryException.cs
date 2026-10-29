namespace Inspecta.Application.Exceptions;

/// <summary>An email could not be handed to the mail server. The message is safe to show to the user.</summary>
public sealed class EmailDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
