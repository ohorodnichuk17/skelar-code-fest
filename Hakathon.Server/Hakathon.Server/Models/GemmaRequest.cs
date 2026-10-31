namespace Hakathon.Server.Models;

public sealed class GemmaRequest
{
    public required GemmaContent[] Contents { get; set; }
    public GemmaGenerationConfig? GenerationConfig { get; set; }
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
public sealed class GemmaGenerationConfig
{
    public double? Temperature { get; set; }
    public string? ResponseMimeType { get; set; }
    public GemmaThinkingConfig? ThinkingConfig { get; set; }
}
public sealed class GemmaThinkingConfig
{
    public required string ThinkingLevel { get; set; }
}
