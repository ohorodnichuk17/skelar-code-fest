namespace Hakathon.Server.Models;

public sealed class GemmaRequest
{
    public required GemmaContent[] Contents { get; set; }
}
public sealed class GemmaContent
{
    public required string Role { get; set; }
    public required GemmaPart[] Parts { get; set; }
}
public sealed class GemmaPart
{
    public required string Text { get; set; }
}

