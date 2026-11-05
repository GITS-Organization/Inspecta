using System.Diagnostics;
using Inspecta.Application.Pdf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Inspecta.Infrastructure.Pdf;

public sealed class PdfGenerator(IServiceProvider services, ILogger<PdfGenerator> logger) : IPdfGenerator
{
    /// <summary>How many documents are drawn at the same time. Drawing is CPU bound and uses native memory, so a small server takes two.</summary>
    public const int MaxConcurrentRenders = 2;

    private static readonly TimeSpan WaitForARender = TimeSpan.FromSeconds(60);

    // One gate for the whole process: the generator is a singleton, and approving a job draws many certificates.
    private readonly SemaphoreSlim renders = new(MaxConcurrentRenders, MaxConcurrentRenders);

    public PdfFile Generate<TModel>(TModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var template = services.GetService<IPdfTemplate<TModel>>()
            ?? throw new PdfGenerationException($"No PDF template is registered for {typeof(TModel).Name}.");

        if (!renders.Wait(WaitForARender))
            throw new PdfGenerationException("The server is busy drawing other documents. Try again in a moment.");
        var timer = Stopwatch.StartNew();
        try
        {
            var content = template.Render(model);
            var file = new PdfFile(content, template.FileName(model));
            logger.LogInformation(
                "Generated PDF {FileName} ({Bytes} bytes) in {ElapsedMs} ms.",
                file.FileName,
                content.Length,
                timer.ElapsedMilliseconds);
            return file;
        }
        catch (Exception exception) when (exception is not PdfGenerationException)
        {
            logger.LogError(exception, "Could not generate a PDF for {Model}.", typeof(TModel).Name);
            throw new PdfGenerationException($"The {typeof(TModel).Name} PDF could not be generated.", exception);
        }
        finally
        {
            renders.Release();
        }
    }
}
