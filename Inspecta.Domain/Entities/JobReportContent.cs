namespace Inspecta.Domain.Entities;

/// <summary>The bytes of a job report file, kept apart from the file's row so that listing reports stays light. Only ever read through its file.</summary>
public sealed class JobReportContent
{
    private JobReportContent() { }

    public JobReportContent(Guid jobReportFileId, byte[] bytes)
    {
        JobReportFileId = jobReportFileId;
        Bytes = bytes;
    }

    public Guid JobReportFileId { get; private set; }
    public byte[] Bytes { get; private set; } = [];
}
