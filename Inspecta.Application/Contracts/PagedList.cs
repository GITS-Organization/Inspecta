namespace Inspecta.Application.Contracts;

/// <summary>One page of a list and how many there are in all.</summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int Total);
