using System.Text;

namespace WhisperDesk.Services;

// File writes are synchronous so the last lines survive closing the window.
internal sealed class RunLog : IProgress<string>, IDisposable
{
    private readonly StreamWriter _writer;
    private readonly IProgress<string> _display;
    private readonly object _gate = new();
    private bool _writeFailed;

    public RunLog(string path, IProgress<string> display)
    {
        _display = display;
        _writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
    }

    public void Report(string value)
    {
        lock (_gate)
        {
            if (!_writeFailed)
            {
                try { _writer.WriteLine($"[{DateTimeOffset.Now:O}] {value}"); }
                catch (IOException ex)
                {
                    _writeFailed = true;
                    _display.Report($"ログの保存に失敗しました: {ex.Message}");
                }
            }
            _display.Report(value);
        }
    }

    public void Dispose()
    {
        try { _writer.Dispose(); }
        catch (IOException ex) { _display.Report($"ログを閉じる際にエラーが発生しました: {ex.Message}"); }
    }
}
