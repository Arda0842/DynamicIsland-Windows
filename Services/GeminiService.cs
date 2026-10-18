using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DynamicIsland.Services;

public class GeminiResult
{
    public string UserText { get; set; } = "";
    public string Action { get; set; } = "NONE";
    public string ActionArg { get; set; } = "";
    public string ResponseMessage { get; set; } = "";
    public bool Success { get; set; } = false;
    public string Error { get; set; } = "";
}

public class GeminiService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(25) };
    private string _apiKey = "";

    private readonly string _configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DynamicIsland",
        "config.json"
    );

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            _apiKey = value?.Trim() ?? "";
            SaveApiKey(_apiKey);
        }
    }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(_apiKey);

    public GeminiService()
    {
        LoadApiKey();
    }

    private void LoadApiKey()
    {
        // 1. Try environment variable
        var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            _apiKey = envKey.Trim();
            return;
        }

        // 2. Try saved config file
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var node = JsonNode.Parse(json);
                _apiKey = node?["apiKey"]?.GetValue<string>()?.Trim() ?? "";
            }
        }
        catch { }
    }

    private void SaveApiKey(string key)
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var obj = new JsonObject { ["apiKey"] = key };
            File.WriteAllText(_configPath, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private const string SYSTEM_PROMPT =
@"Sen Windows için geliştirilmiş 'Dinamik Ada' (Dynamic Island) uygulamasının akıllı Türkçe ses ve AI asistanısın.
Kullanıcı mikrofondan konuştu veya mesaj yazdı.

GÖREVİN:
1. Kullanıcının söylediği/yazdığı cümleyi analiz et.
2. Aşağıdaki komutlardan biriyle eşleşiyorsa uygun ACTION değerini belirle:
   - Müzik arama veya açma (ör: 'semicenk müziklerinden bir tane aç', 'şarkı aç', 'tarkan çal'):
     ACTION: PLAY_MUSIC: <sanatçı veya şarkı adı>
   - Ses yükseltme (ör: 'sesi aç', 'sesi yükselt', 'müziğin sesini aç'):
     ACTION: VOLUME_UP
   - Ses kısma (ör: 'sesi kıs', 'sesi azalt'):
     ACTION: VOLUME_DOWN
   - Sessize alma / sesi kapatma (ör: 'sesi kapat', 'sessize al', 'mute yap'):
     ACTION: VOLUME_MUTE
   - Sonraki şarkı / müziği değiştir (ör: 'müziği değiştir', 'sonraki şarkı', 'geç'):
     ACTION: NEXT_TRACK
   - Önceki şarkı (ör: 'önceki şarkı', 'başa sar'):
     ACTION: PREV_TRACK
   - Durdur veya Başlat (ör: 'müziği durdur', 'durdur', 'devam et', 'oynat'):
     ACTION: TOGGLE_PLAY
   - Kronometre başlat (ör: 'kronometreyi başlat'):
     ACTION: START_STOPWATCH
   - Kronometre durdur (ör: 'kronometreyi durdur'):
     ACTION: PAUSE_STOPWATCH
   - Kronometre sıfırla (ör: 'kronometreyi sıfırla'):
     ACTION: RESET_STOPWATCH
   - Geri sayım / zamanlayıcı başlat (ör: '5 dakika geri sayım başlat', '3 dakika süre tut'):
     ACTION: START_TIMER: <dakika sayısı>
   - Genel bir soru sorduysa veya sohbet etmek istiyorsa:
     ACTION: NONE

Çıktı formatın her zaman şu şekilde olmalıdır:
USER_TEXT: <Kullanıcının söylediği veya yazdığı cümle>
ACTION: <Yukarıdaki eylemlerden biri veya NONE>
MESSAGE: <Kullanıcıya vereceğin kısa, net ve samimi Türkçe yanıt veya soru cevabı>";

    public async Task<GeminiResult> ProcessTextAsync(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new GeminiResult
            {
                Success = false,
                Error = "Gemini API anahtarı girilmedi. Lütfen Ayarlar kısmından API anahtarınızı girin."
            };
        }

        try
        {
            var requestBody = new JsonObject
            {
                ["contents"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["parts"] = new JsonArray
                        {
                            new JsonObject { ["text"] = $"{SYSTEM_PROMPT}\n\nKullanıcı girdisi:\n{userPrompt}" }
                        }
                    }
                }
            };

            return await CallGeminiApiAsync(requestBody);
        }
        catch (Exception ex)
        {
            return new GeminiResult { Success = false, Error = $"Hata: {ex.Message}" };
        }
    }

    public async Task<GeminiResult> ProcessAudioAsync(byte[] wavBytes)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new GeminiResult
            {
                Success = false,
                Error = "Gemini API anahtarı girilmedi. Lütfen Ayarlar kısmından API anahtarınızı girin."
            };
        }

        try
        {
            var base64Audio = Convert.ToBase64String(wavBytes);

            var requestBody = new JsonObject
            {
                ["contents"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["parts"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["inline_data"] = new JsonObject
                                {
                                    ["mime_type"] = "audio/wav",
                                    ["data"] = base64Audio
                                }
                            },
                            new JsonObject { ["text"] = SYSTEM_PROMPT }
                        }
                    }
                }
            };

            return await CallGeminiApiAsync(requestBody);
        }
        catch (Exception ex)
        {
            return new GeminiResult { Success = false, Error = $"Ses işleme hatası: {ex.Message}" };
        }
    }

    private async Task<GeminiResult> CallGeminiApiAsync(JsonObject body)
    {
        var models = new[] { "gemini-2.5-flash", "gemini-1.5-flash" };
        string lastError = "";

        foreach (var model in models)
        {
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_apiKey}";
                var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var respStr = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"API Hatası ({response.StatusCode}): {respStr}";
                    continue;
                }

                var root = JsonNode.Parse(respStr);
                var text = root?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return ParseGeminiResponse(text);
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return new GeminiResult { Success = false, Error = lastError };
    }

    private GeminiResult ParseGeminiResponse(string rawText)
    {
        var result = new GeminiResult { Success = true };
        var lines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var messageBuilder = new StringBuilder();
        bool capturingMessage = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("USER_TEXT:", StringComparison.OrdinalIgnoreCase))
            {
                result.UserText = trimmed.Substring("USER_TEXT:".Length).Trim();
            }
            else if (trimmed.StartsWith("ACTION:", StringComparison.OrdinalIgnoreCase))
            {
                var actionStr = trimmed.Substring("ACTION:".Length).Trim();
                if (actionStr.Contains(':'))
                {
                    var parts = actionStr.Split(':', 2);
                    result.Action = parts[0].Trim();
                    result.ActionArg = parts[1].Trim();
                }
                else
                {
                    result.Action = actionStr;
                }
            }
            else if (trimmed.StartsWith("MESSAGE:", StringComparison.OrdinalIgnoreCase))
            {
                capturingMessage = true;
                messageBuilder.AppendLine(trimmed.Substring("MESSAGE:".Length).Trim());
            }
            else if (capturingMessage)
            {
                messageBuilder.AppendLine(trimmed);
            }
        }

        result.ResponseMessage = messageBuilder.ToString().Trim();

        // Fallback if formatting was loose
        if (string.IsNullOrWhiteSpace(result.ResponseMessage))
        {
            result.ResponseMessage = rawText.Trim();
        }

        return result;
    }

    public async Task<string> AskDirectAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) return "";

        var models = new[] { "gemini-2.5-flash", "gemini-1.5-flash" };
        var body = new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["parts"] = new JsonArray
                    {
                        new JsonObject { ["text"] = prompt }
                    }
                }
            }
        };

        foreach (var model in models)
        {
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_apiKey}";
                var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    var respStr = await response.Content.ReadAsStringAsync();
                    var root = JsonNode.Parse(respStr);
                    var text = root?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
                }
            }
            catch { }
        }
        return "";
    }
}
