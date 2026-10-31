namespace Hakathon.Server.Options;

public class GemmaOption
{
    public required string API_Key { get; set; }
    public required string AI_Model { get; set; }
    public double AI_TimeoutSeconds { get; set; } = 4;
}
