namespace Inspecta.Application.Interfaces;

/// <summary>Runs work in one database transaction.</summary>
public interface ITransactionRunner
{
    /// <summary>Everything the work saves is kept together; when it throws, none of it is.</summary>
    Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default);
}
