namespace Hakathon.Server.Models;

public sealed class GemmaResponse
{
    public Candidate[]? Candidates { get; set; }
}

public sealed class Candidate
{
    public Content? Content { get; set; }
}
public sealed class Content
{
    public Part[]? Parts { get; set; }
}
public sealed class Part
{
    public string? Text { get; set; }
    // Gemma 4 повертає "роздуми" окремою частиною з thought = true — їх не показуємо й не парсимо.
    public bool? Thought { get; set; }
}
