namespace Inspecta.Application.Pdf;

/// <summary>Turns one kind of data model into a PDF. Implementations live in the infrastructure layer.</summary>
public interface IPdfTemplate<in TModel>
{
    string FileName(TModel model);

    byte[] Render(TModel model);
}
