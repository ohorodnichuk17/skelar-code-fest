using Hakathon.Server.Models;

namespace Hakathon.Server.Services;


internal static class GemmaRequestFactory
{
    public static GemmaRequest CreateRequest(string Prompt)
    {
        return new GemmaRequest()
        {
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
