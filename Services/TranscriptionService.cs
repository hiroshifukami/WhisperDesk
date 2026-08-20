using System.Diagnostics;
using System.Text;

namespace WhisperDesk.Services;

public sealed record TranscriptionRequest(
    string InputPath,
    string OutputBasePath,
    string WhisperPath,
    string FfmpegPath,
    string ModelPath,
    string Language,
    bool OutputTxt,
    bool OutputSrt,
    bool OutputVtt);

public sealed class TranscriptionService
{
    private Process? _activeProcess;

    public async Task RunAsync(
        TranscriptionRequest request,
        IProgress<string> log,
        CancellationToken cancellationToken)
    {
        Validate(request);
        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputBasePath)!);

        var temporaryWav = Path.Combine(Path.GetTempPath(), $"WhisperDesk-{Guid.NewGuid():N}.wav");
        try
        {
            log.Report("音声をWhisper用WAVへ変換しています…");
            await RunProcessAsync(request.FfmpegPath,
                ["-hide_banner", "-y", "-i", request.InputPath, "-vn", "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", temporaryWav],
                log, cancellationToken);

            log.Report("文字起こしを開始します…");
            var args = new List<string>
            {
                "-m", request.ModelPath,
                "-f", temporaryWav,
                "-l", request.Language,
                "-of", request.OutputBasePath
            };
            if (request.OutputTxt) args.Add("-otxt");
            if (request.OutputSrt) args.Add("-osrt");
            if (request.OutputVtt) args.Add("-ovtt");

            await RunProcessAsync(request.WhisperPath, args, log, cancellationToken);
            log.Report("文字起こしが完了しました。");
        }
        finally
        {
            TryDelete(temporaryWav);
            _activeProcess = null;
        }
    }

    public void Cancel()
    {
        try
        {
            if (_activeProcess is { HasExited: false })
                _activeProcess.Kill(entireProcessTree: true);
        }
        catch
        {
            // 終了とキャンセルが競合した場合は無視する。
        }
    }

    private async Task RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        IProgress<string> log,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _activeProcess = process;
        process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log.Report(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log.Report(e.Data); };

        if (!process.Start())
            throw new InvalidOperationException($"起動できませんでした: {executable}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        });

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"外部プログラムが終了コード {process.ExitCode} を返しました: {Path.GetFileName(executable)}");
    }

    private static void Validate(TranscriptionRequest request)
    {
        if (!File.Exists(request.InputPath)) throw new FileNotFoundException("入力ファイルが見つかりません。", request.InputPath);
        if (!File.Exists(request.WhisperPath)) throw new FileNotFoundException("whisper-cli.exeが見つかりません。設定を確認してください。", request.WhisperPath);
        if (!File.Exists(request.ModelPath)) throw new FileNotFoundException("モデルが見つかりません。", request.ModelPath);
        if (!request.OutputTxt && !request.OutputSrt && !request.OutputVtt)
            throw new InvalidOperationException("出力形式を1つ以上選んでください。");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
