using System;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DynamicIsland.Services;

public class TranslateService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly GeminiService _geminiService;

    public TranslateService(GeminiService geminiService)
    {
        _geminiService = geminiService;
    }

    public async Task<string> TranslateAsync(string text, string fromLang, string toLang)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = text.Trim();

        // 1. First choice: Gemini AI if API key is present for smart context-aware translation
        if (_geminiService.HasApiKey)
        {
            try
            {
                string fromName = fromLang.ToLowerInvariant() == "tr" ? "Türkçe" : "İngilizce";
                string toName = toLang.ToLowerInvariant() == "tr" ? "Türkçe" : "İngilizce";
                string prompt = $"Aşağıdaki {fromName} metni akıcı ve doğal bir {toName}ye çevir. Yanıt olarak YALNIZCA çevrilmiş cümleyi ver, açıklama yapma, tırnak ekleme:\n\n{text}";

                var aiResult = await _geminiService.AskDirectAsync(prompt);
                if (!string.IsNullOrWhiteSpace(aiResult) &&
                    !aiResult.StartsWith("Hata:") &&
                    !aiResult.StartsWith("Lütfen") &&
                    !aiResult.Contains("API"))
                {
                    return aiResult.Trim(' ', '"', '\'', '\n', '\r');
                }
            }
            catch { }
        }

        // 2. Second choice: MyMemory free translation API (no key required)
        try
        {
            string pair = $"{fromLang.ToLowerInvariant()}|{toLang.ToLowerInvariant()}";
            string url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={pair}";
            var json = await _http.GetStringAsync(url);
            var node = JsonNode.Parse(json);
            var translated = node?["responseData"]?["translatedText"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(translated))
            {
                var clean = System.Net.WebUtility.HtmlDecode(translated);
                if (!clean.StartsWith("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase))
                {
                    return clean;
                }
            }
        }
        catch { }

        // 3. Fallback: Common offline gamer & conversation dictionary
        return GetOfflineFallback(text, fromLang, toLang);
    }

    private static string GetOfflineFallback(string text, string from, string to)
    {
        var lower = text.ToLowerInvariant().Trim();
        if (from.Equals("tr", StringComparison.OrdinalIgnoreCase))
        {
            return lower switch
            {
                "merhaba" or "selam" => "Hello / Hi",
                "nasılsın" or "nasılsın?" => "How are you?",
                "iyiyim" => "I'm good",
                "teşekkürler" or "sağol" => "Thanks / Thank you",
                "evet" => "Yes",
                "hayır" => "No",
                "lütfen" => "Please",
                "özür dilerim" or "kusura bakma" => "Sorry",
                "iyi oyunlar" or "iyi oyundu" => "Good game / GG",
                "tamam" or "olur" => "OK / Sure",
                "yardım et" or "yardım" => "Help me",
                "bana silah at" or "silah at" => "Drop me a gun",
                "gel" or "buraya gel" => "Come here",
                "git" or "kaç" => "Run / Go",
                "bekle" => "Wait",
                "düşman gördüm" or "düşman" => "Enemy spotted",
                "dikkat et" => "Be careful",
                _ => text
            };
        }
        else
        {
            return lower switch
            {
                "hello" or "hi" or "hey" => "Merhaba / Selam",
                "how are you" or "how are you?" => "Nasılsın?",
                "i am good" or "i'm good" => "İyiyim",
                "thanks" or "thank you" or "thx" => "Teşekkürler",
                "yes" or "yeah" or "yep" => "Evet",
                "no" or "nope" => "Hayır",
                "please" or "pls" => "Lütfen",
                "sorry" or "sry" => "Özür dilerim",
                "good game" or "gg" or "gg wp" => "İyi oyundu (GG)",
                "ok" or "okay" => "Tamam",
                "help" or "help me" => "Yardım et",
                "drop" or "drop me a gun" => "Silah at",
                "wait" => "Bekle",
                "enemy spotted" => "Düşman görüldü",
                "be careful" => "Dikkat et",
                _ => text
            };
        }
    }
}
