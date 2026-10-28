namespace Inspecta.Domain.Enums;

/// <summary>Whether an inbox email is a real request or only a candidate waiting for the coordinator.</summary>
public enum RequestTriage
{
    Request = 1,
    Maybe = 2
}
