using System.Text;

namespace ChartForge.Infrastructure.Services;
public sealed class StreamParseState
{
    public WorkflowNodeType ActiveNode { get; set; } = WorkflowNodeType.Unknown;
    public bool IsInsideCodeBlock { get; set; }
    public StringBuilder ChartCodeBuilder { get; } = new();
    public StringBuilder SqlBuffer { get; } = new StringBuilder();

    // MainAgent: buffer THOUGHT block to extract <TITLE>; content is always suppressed.
    public StringBuilder ThoughtBuffer { get; } = new();
    public bool TitleExtracted { get; set; }

    // OutputNode: defensive THOUGHT filter + clean content streaming.
    public StringBuilder OutputBuffer { get; } = new();
    public bool OutputThoughtClosed { get; set; }
    public bool SqlClosed { get; set; }

    // DATA block detection across chunks.
    public StringBuilder DataBuffer { get; } = new();
    public bool DataBlockActive { get; set; }
}