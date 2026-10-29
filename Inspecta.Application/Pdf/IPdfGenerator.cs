namespace Inspecta.Application.Pdf;

/// <summary>
/// Single entry point for every PDF the application produces. The template registered for the model type
/// decides the layout, so callers only deal with plain data models.
/// </summary>
public interface IPdfGenerator
{
    /// <exception cref="PdfGenerationException">No template is registered for the model, or rendering failed.</exception>
    PdfFile Generate<TModel>(TModel model);
}
