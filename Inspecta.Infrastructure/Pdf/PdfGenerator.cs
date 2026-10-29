using System.Diagnostics;
using Inspecta.Application.Pdf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Inspecta.Infrastructure.Pdf;

public sealed class PdfGenerator(IServiceProvider services, ILogger<PdfGenerator> logger) : IPdfGenerator
{
    public PdfFile Generate<TModel>(TModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var template = services.GetService<IPdfTemplate<TModel>>()
            ?? throw new PdfGenerationException($"No PDF template is registered for {typeof(TModel).Name}.");

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
    }
}
