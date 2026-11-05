using Microsoft.AspNetCore.Mvc;
using Inspecta.Application.Contracts;

namespace Inspecta.Api;

/// <summary>
/// The optional limit and offset of a list. Asked without them a list answers in full as it always did (up to a ceiling that
/// keeps one request bounded); the total of all the rows is always in the X-Total-Count header.
/// </summary>
public static class PageRequest
{
    public const string TotalHeader = "X-Total-Count";
    public const int DefaultLimit = 2000;
    public const int MaxLimit = 5000;

    public static (int Offset, int Limit)? Read(int? limit, int offset) =>
        offset < 0 || limit is < 1 or > MaxLimit ? null : (offset, limit ?? DefaultLimit);

    public static ActionResult Invalid(ControllerBase controller) =>
        controller.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            detail: $"limit must be between 1 and {MaxLimit} and offset cannot be negative.");

    public static ActionResult<IReadOnlyList<T>> Respond<T>(ControllerBase controller, PagedList<T> page)
    {
        controller.Response.Headers[TotalHeader] = page.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return controller.Ok(page.Items);
    }
}
