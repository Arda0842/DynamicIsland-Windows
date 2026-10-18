using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DynamicIsland.Services;

public class Cs2GsiService : IDisposable
{
    private const int GSI_PORT = 13371;
    private readonly string _prefix = $"http://127.0.0.1:{GSI_PORT}/";
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    // Precision countdown timer for smooth UI ticks (100ms interval)
    private readonly DispatcherTimer _timer = new();

    public bool IsListening => _listener?.IsListening ?? false;
    public bool IsBombPlanted { get; private set; } = false;
    public string BombState { get; private set; } = "idle"; // "idle", "planted", "defusing", "defused", "exploded"
    public double TimeRemaining { get; private set; } = 0.0;
    public double TotalBombTime { get; set; } = 40.0; // Standard competitive bomb timer

    // Defuse limits
    public const double DEFUSE_TIME_WITHOUT_KIT = 10.0;
    public const double DEFUSE_TIME_WITH_KIT = 5.0;

    public bool CanDefuseWithoutKit => TimeRemaining > DEFUSE_TIME_WITHOUT_KIT;
    public bool CanDefuseWithKit => TimeRemaining > DEFUSE_TIME_WITH_KIT;
    public double KitsizDeadline => Math.Max(0, TimeRemaining - DEFUSE_TIME_WITHOUT_KIT);
    public double KitliDeadline => Math.Max(0, TimeRemaining - DEFUSE_TIME_WITH_KIT);

    public bool EnableAudioAlerts { get; set; } = true;
    public bool AutoExpandOnPlant { get; set; } = false;

    private bool _beep10Played = false;
    private bool _beep5Played = false;

    // Events
    public event Action? BombPlanted;
    public event Action<double, bool, bool, double, double>? BombTick; // timeRemaining, canWithoutKit, canWithKit, kitsizDeadline, kitliDeadline
    public event Action? BombDefused;
    public event Action? BombExploded;
    public event Action? BombReset;
    public event Action<string>? GsiStatusChanged;

    public string LastMapName { get; private set; } = "";
    public string LastRoundPhase { get; private set; } = "";
    public DateTime LastPacketReceived { get; private set; } = DateTime.MinValue;

    public Cs2GsiService()
    {
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += Timer_Tick;
    }

    public void StartListener()
    {
        if (_listener != null && _listener.IsListening) return;

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:13371/");
            try { _listener.Prefixes.Add("http://localhost:13371/"); } catch { }
            _listener.Start();
            GsiStatusChanged?.Invoke("🟡 GSI Port 13371 Hazır (CS2 Bekleniyor)");

            Task.Run(() => ListenLoopAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            GsiStatusChanged?.Invoke($"⚠️ GSI Port Hatası: {ex.Message}");
        }
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessRequestAsync(context), token);
            }
            catch
            {
                if (token.IsCancellationRequested) break;
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            string body;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            // Immediately send 200 OK back to CS2 with ContentLength64 = 0
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentLength64 = 0;
            context.Response.Close();

            if (!string.IsNullOrWhiteSpace(body))
            {
                LastPacketReceived = DateTime.Now;
                ParseGsiJson(body);
            }
        }
        catch { }
    }

    public void ParseGsiJson(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node == null) return;

            var bombNode = node["bomb"];
            var roundNode = node["round"];
            var mapNode = node["map"];
            var phaseCdNode = node["phase_countdowns"];

            if (mapNode?["name"] != null)
            {
                LastMapName = mapNode["name"]!.ToString();
            }

            string? bState = bombNode?["state"]?.ToString()?.Trim()?.ToLowerInvariant();
            string? rBomb = roundNode?["bomb"]?.ToString()?.Trim()?.ToLowerInvariant();
            string? rPhase = roundNode?["phase"]?.ToString()?.Trim()?.ToLowerInvariant();
            string? pcPhase = phaseCdNode?["phase"]?.ToString()?.Trim()?.ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(rPhase))
            {
                LastRoundPhase = rPhase;
            }

            double? countdownVal = null;
            if (bombNode?["countdown"] != null)
            {
                if (double.TryParse(bombNode["countdown"]!.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cd))
                {
                    countdownVal = cd;
                }
            }
            else if (phaseCdNode?["phase_ends_in"] != null)
            {
                if (double.TryParse(phaseCdNode["phase_ends_in"]!.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cd2))
                {
                    countdownVal = cd2;
                }
            }

            // Update status text with map & time
            string mapDisplay = string.IsNullOrWhiteSpace(LastMapName) ? "" : $" • {LastMapName}";
            GsiStatusChanged?.Invoke($"🟢 CS2 Bağlandı ({LastPacketReceived:HH:mm:ss}){mapDisplay}");

            // Check plant state
            bool isPlanted = (bState == "planted" || bState == "defusing" || rBomb == "planted" || pcPhase == "bomb");
            bool isDefused = (bState == "defused" || rBomb == "defused");
            bool isExploded = (bState == "exploded" || rBomb == "exploded");

            if (isDefused)
            {
                TriggerDefused();
            }
            else if (isExploded)
            {
                TriggerExploded();
            }
            else if (isPlanted)
            {
                if (!IsBombPlanted)
                {
                    // Newly planted!
                    TriggerPlanted(countdownVal ?? TotalBombTime);
                }
                else if (countdownVal.HasValue && Math.Abs(TimeRemaining - countdownVal.Value) > 0.6)
                {
                    // Resynchronize with server countdown
                    TimeRemaining = countdownVal.Value;
                }
            }
            else if (rPhase == "freezetime" || rPhase == "over" || (rBomb != "planted" && bState == "idle" && pcPhase != "bomb"))
            {
                if (IsBombPlanted)
                {
                    ResetBombState();
                }
            }
        }
        catch { }
    }

    public void TriggerPlanted(double initialTime = 40.0)
    {
        IsBombPlanted = true;
        BombState = "planted";
        TimeRemaining = initialTime;
        _beep10Played = false;
        _beep5Played = false;

        App.Current.Dispatcher.Invoke(() =>
        {
            _timer.Start();
            BombPlanted?.Invoke();
            BombTick?.Invoke(TimeRemaining, CanDefuseWithoutKit, CanDefuseWithKit, KitsizDeadline, KitliDeadline);
        });
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!IsBombPlanted) return;

        TimeRemaining -= 0.1;
        if (TimeRemaining < 0) TimeRemaining = 0;

        // Sound Alerts at critical thresholds
        if (EnableAudioAlerts)
        {
            if (!_beep10Played && TimeRemaining <= 10.0 && TimeRemaining > 9.5)
            {
                _beep10Played = true;
                PlayAlertTone(880, 150); // Crisp high alert: Kitsiz threshold passed!
            }
            else if (!_beep5Played && TimeRemaining <= 5.0 && TimeRemaining > 4.5)
            {
                _beep5Played = true;
                PlayAlertTone(440, 200, dual: true); // Danger dual tone: Kitli threshold passed, RUN!
            }
        }

        BombTick?.Invoke(TimeRemaining, CanDefuseWithoutKit, CanDefuseWithKit, KitsizDeadline, KitliDeadline);

        if (TimeRemaining <= 0)
        {
            TriggerExploded();
        }
    }

    public void TriggerDefused()
    {
        if (BombState == "defused") return;
        _timer.Stop();
        IsBombPlanted = false;
        BombState = "defused";

        App.Current.Dispatcher.Invoke(() =>
        {
            BombDefused?.Invoke();
        });

        // Auto reset after 5 seconds
        Task.Delay(5000).ContinueWith(_ =>
        {
            if (BombState == "defused") ResetBombState();
        });
    }

    public void TriggerExploded()
    {
        if (BombState == "exploded") return;
        _timer.Stop();
        IsBombPlanted = false;
        BombState = "exploded";
        TimeRemaining = 0;

        App.Current.Dispatcher.Invoke(() =>
        {
            BombExploded?.Invoke();
        });

        // Auto reset after 5 seconds
        Task.Delay(5000).ContinueWith(_ =>
        {
            if (BombState == "exploded") ResetBombState();
        });
    }

    public void ResetBombState()
    {
        _timer.Stop();
        IsBombPlanted = false;
        BombState = "idle";
        TimeRemaining = 0;
        _beep10Played = false;
        _beep5Played = false;

        App.Current.Dispatcher.Invoke(() =>
        {
            BombReset?.Invoke();
        });
    }

    private static void PlayAlertTone(int freq, int duration, bool dual = false)
    {
        Task.Run(() =>
        {
            try
            {
                Console.Beep(freq, duration);
                if (dual)
                {
                    Thread.Sleep(60);
                    Console.Beep(freq, duration + 50);
                }
            }
            catch { }
        });
    }

    // --- Mock / Test Simulation for Instant Testing Without CS2 ---
    public void StartTestSimulation()
    {
        ResetBombState();
        TriggerPlanted(40.0);
    }

    public void StopTestSimulation()
    {
        ResetBombState();
    }

    // --- CS2 GSI CFG Auto-Installer ---
    public static string? FindCs2CfgPath()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var steamPath = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                var candidate = Path.Combine(steamPath, "steamapps", "common", "Counter-Strike Global Offensive", "game", "csgo", "cfg");
                if (Directory.Exists(candidate)) return candidate;
            }
        }
        catch { }

        string[] fallbacks = [
            @"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg",
            @"D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg",
            @"E:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg",
            @"C:\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg",
            @"D:\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg"
        ];

        foreach (var fb in fallbacks)
        {
            if (Directory.Exists(fb)) return fb;
        }

        return null;
    }

    public static string GsiConfigContent => """"
"DynamicIsland GSI"
{
    "uri" "http://127.0.0.1:13371"
    "timeout" "5.0"
    "buffer"  "0.1"
    "throttle" "0.1"
    "heartbeat" "10.0"
    "data"
    {
        "provider"            "1"
        "map"                 "1"
        "round"               "1"
        "player_id"           "1"
        "player_state"        "1"
        "player_weapons"      "1"
        "player_match_stats"  "1"
        "bomb"                "1"
        "phase_countdowns"    "1"
    }
}
"""";

    public static bool EnsureGsiConfigInstalled(out string message)
    {
        var cfgDir = FindCs2CfgPath();
        if (cfgDir == null)
        {
            message = "Counter-Strike 2 oyun klasörü bulunamadı.";
            return false;
        }

        try
        {
            var targetFile = Path.Combine(cfgDir, "gamestate_integration_dynamicisland.cfg");
            File.WriteAllText(targetFile, GsiConfigContent);

            // Also install to Steam userdata configs if found
            try
            {
                var steamRoot = Path.GetFullPath(Path.Combine(cfgDir, @"..\..\..\..\.."));
                var userdataDir = Path.Combine(steamRoot, "userdata");
                if (Directory.Exists(userdataDir))
                {
                    foreach (var userFolder in Directory.GetDirectories(userdataDir))
                    {
                        var userCfgDir = Path.Combine(userFolder, "730", "local", "cfg");
                        if (Directory.Exists(userCfgDir))
                        {
                            File.WriteAllText(Path.Combine(userCfgDir, "gamestate_integration_dynamicisland.cfg"), GsiConfigContent);
                        }
                    }
                }
            }
            catch { }

            message = "gamestate_integration_dynamicisland.cfg başarıyla kuruldu!";
            return true;
        }
        catch (Exception ex)
        {
            message = $"CFG yazılırken hata oluştu: {ex.Message}";
            return false;
        }
    }

    public static bool IsGsiConfigInstalled()
    {
        var cfgDir = FindCs2CfgPath();
        if (cfgDir == null) return false;
        return File.Exists(Path.Combine(cfgDir, "gamestate_integration_dynamicisland.cfg"));
    }

    public void Dispose()
    {
        _timer.Stop();
        _cts?.Cancel();
        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }
    }
}
