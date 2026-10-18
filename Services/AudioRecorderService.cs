using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DynamicIsland.Services;

public class AudioRecorderService
{
    [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi)]
    private static extern int mciSendString(string command, StringBuilder? buffer, int bufferSize, IntPtr hwndCallback);

    private string? _currentTempFile;
    public bool IsRecording { get; private set; }

    public bool StartRecording()
    {
        try
        {
            if (IsRecording)
            {
                StopRecordingSilently();
            }

            _currentTempFile = Path.Combine(Path.GetTempPath(), $"island_voice_{Guid.NewGuid():N}.wav");

            mciSendString("close recsound", null, 0, IntPtr.Zero);
            mciSendString("open new type waveaudio alias recsound", null, 0, IntPtr.Zero);
            mciSendString("set recsound bitspersample 16 channels 1 samplespersec 16000", null, 0, IntPtr.Zero);
            mciSendString("record recsound", null, 0, IntPtr.Zero);

            IsRecording = true;
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Recording start error: {ex.Message}");
            IsRecording = false;
            return false;
        }
    }

    public async Task<byte[]?> StopRecordingAsync()
    {
        if (!IsRecording || string.IsNullOrEmpty(_currentTempFile))
        {
            return null;
        }

        try
        {
            mciSendString($"save recsound \"{_currentTempFile}\"", null, 0, IntPtr.Zero);
            mciSendString("close recsound", null, 0, IntPtr.Zero);
            IsRecording = false;

            // Wait brief moment for filesystem to flush
            await Task.Delay(100);

            if (File.Exists(_currentTempFile))
            {
                var bytes = await File.ReadAllBytesAsync(_currentTempFile);
                try { File.Delete(_currentTempFile); } catch { }
                return bytes.Length > 1000 ? bytes : null; // ignore empty audio
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Recording stop error: {ex.Message}");
        }
        finally
        {
            IsRecording = false;
        }

        return null;
    }

    private void StopRecordingSilently()
    {
        try
        {
            mciSendString("close recsound", null, 0, IntPtr.Zero);
            if (!string.IsNullOrEmpty(_currentTempFile) && File.Exists(_currentTempFile))
            {
                File.Delete(_currentTempFile);
            }
        }
        catch { }
        IsRecording = false;
    }
}
