using WhisperDesk.Services;
using WhisperDesk.Models;
using System.Text.Json;

// The same executable acts as a controllable FFmpeg/Whisper child process.
if (args.Length > 0)
{
    string Value(string key) => args[Array.IndexOf(args, key) + 1];
    if (args.Contains("--help"))
    {
        Console.Error.WriteLine("--max-context N --vad --vad-model FNAME");
        return 1;
    }
    if (args.Contains("-i"))
    {
        File.Copy(Value("-i"), args[^1]);
        return 0;
    }
    string input = Value("-f");
    string mode = File.ReadAllText(input);
    if (mode == "cancel")
    {
        using var lockedWav = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.None);
        Console.WriteLine("LOCKED:" + input);
        Console.Out.Flush();
        await Task.Delay(TimeSpan.FromSeconds(30));
    }
    if (mode != "missing")
        File.WriteAllText(Value("-of") + ".txt", mode == "empty" ? "" : "transcript");
    Console.WriteLine("FAKE transcription finished");
    return 0;
}

string root = Path.Combine(Path.GetTempPath(), "WhisperDesk-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    count++;
    Console.WriteLine("PASS: " + name);
}
var executable = Environment.ProcessPath!;
var model = Path.Combine(root, "model.bin");
File.WriteAllText(model, "fixture");
TranscriptionRequest Request(string mode, bool srt = false)
{
    string input = Path.Combine(root, Guid.NewGuid().ToString("N") + ".wav");
    File.WriteAllText(input, mode);
    return new(input, Path.Combine(root, Guid.NewGuid().ToString("N")), executable,
        executable, model, "ja", true, srt, false);
}
async Task MustFail(TranscriptionRequest request)
{
    try { await new TranscriptionService().RunAsync(request, new TestLog(), default); }
    catch (IOException) { return; }
    throw new Exception("Expected output failure");
}
try
{
    var normal = Request("normal");
    await new TranscriptionService().RunAsync(normal, new TestLog(), default);
    Check(File.ReadAllText(normal.OutputBasePath + ".txt") == "transcript", "Normal output published");
    var savedLog = File.ReadAllText(normal.OutputBasePath + ".log");
    Check(savedLog.Contains("FAKE transcription finished") && savedLog.Contains("総処理時間"), "Persistent log contains final child output and duration");

    var empty = Request("empty");
    await new TranscriptionService().RunAsync(empty, new TestLog(), default);
    Check(new FileInfo(empty.OutputBasePath + ".txt").Length == 0, "Silence may produce an empty transcript");

    var missing = Request("missing");
    await MustFail(missing);
    Check(File.ReadAllText(missing.OutputBasePath + ".log").Contains("生成されません"), "Zero exit without output is a failure");

    var partial = Request("normal", srt: true);
    await MustFail(partial);
    Check(!File.Exists(partial.OutputBasePath + ".txt"), "Incomplete set is not published");

    var existing = Request("normal");
    File.WriteAllText(existing.OutputBasePath + ".txt", "keep");
    await MustFail(existing);
    Check(File.ReadAllText(existing.OutputBasePath + ".txt") == "keep", "Existing output preserved");

    var reserved = Request("normal");
    File.WriteAllText(reserved.OutputBasePath + ".log", "keep log");
    await MustFail(reserved);
    Check(File.ReadAllText(reserved.OutputBasePath + ".log") == "keep log", "Run reservation prevents log overwrite");

    var canceled = Request("cancel");
    var cancelLog = new TestLog();
    using var cts = new CancellationTokenSource();
    var task = new TranscriptionService().RunAsync(canceled, cancelLog, cts.Token);
    var wav = await cancelLog.Locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
    cts.Cancel();
    try { await task.WaitAsync(TimeSpan.FromSeconds(10)); throw new Exception("Cancellation expected"); }
    catch (OperationCanceledException) { }
    Check(!File.Exists(wav), "Cancellation reaps process before deleting locked WAV");
    Check(File.ReadAllText(canceled.OutputBasePath + ".log").Contains("中止"), "Cancellation recorded");
    Check(!Directory.EnumerateDirectories(root, ".whisperdesk-*").Any(), "Staging directories cleaned up");

    var stable = Request("normal") with { StableMode = true };
    await new TranscriptionService().RunAsync(stable, new TestLog(), default);
    Check(File.Exists(stable.OutputBasePath + ".txt"), "Stable mode supports legacy help exit code and missing VAD");
    Check(!JsonSerializer.Deserialize<AppSettings>("{}")!.StableMode, "Old settings preserve normal mode");
    Check(StableMode.CreateArguments("--no-context --max-context N", root, new TestLog()).SequenceEqual(new[] { "--no-context" }), "Prefer no-context when supported");
    Console.WriteLine($"{count} checks passed");
    return 0;
}
finally { Directory.Delete(root, recursive: true); }

sealed class TestLog : IProgress<string>
{
    public TaskCompletionSource<string> Locked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Report(string value)
    {
        if (value.StartsWith("LOCKED:")) Locked.TrySetResult(value[7..]);
    }
}
