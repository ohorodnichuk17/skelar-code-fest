using Hakathon.Server.Models;

namespace Hakathon.Server.Services;


internal static class GemmaRequestFactory
{
    public static GemmaRequest CreateRequest(string Prompt, GemmaGenerationConfig? generationConfig = null)
    {
        return new GemmaRequest()
        {
            GenerationConfig = generationConfig,
            Contents = new GemmaContent[]
        {
            new GemmaContent()
            {
                Role = "user",
                Parts = new GemmaPart[]
                {
                    new GemmaPart()
                    {
                        Text = Prompt,
                    }
                }
            }
        }

        };
    }
}
