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
    bool OutputVtt,
    bool StableMode = false);

public sealed class TranscriptionService
{
    private Process? _activeProcess;

    public async Task RunAsync(
        TranscriptionRequest request,
        IProgress<string> log,
        CancellationToken cancellationToken)
    {
        Validate(request);
        cancellationToken.ThrowIfCancellationRequested();
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(request.OutputBasePath))!;
        Directory.CreateDirectory(outputDirectory);
        // CreateNew also reserves this output name across multiple application instances.
        using var runLog = new RunLog(request.OutputBasePath + ".log", log);
        log = runLog;
        var elapsed = Stopwatch.StartNew();
        var stagingDirectory = Path.Combine(outputDirectory, ".whisperdesk-" + Guid.NewGuid().ToString("N"));
        var temporaryWav = Path.Combine(Path.GetTempPath(), $"WhisperDesk-{Guid.NewGuid():N}.wav");
        try
        {
            log.Report($"入力: {request.InputPath}");
            log.Report($"モデル: {request.ModelPath}");
            log.Report($"CLI: {request.WhisperPath}");
            log.Report($"出力: {request.OutputBasePath}");
            log.Report($"安定モード: {request.StableMode}");
            var stableArgs = request.StableMode
                ? StableMode.CreateArguments(await ReadHelpAsync(request.WhisperPath, cancellationToken),
                    Path.GetDirectoryName(Path.GetFullPath(request.ModelPath))!, log)
                : Array.Empty<string>();
            var extensions = new List<string>();
            if (request.OutputTxt) extensions.Add(".txt");
            if (request.OutputSrt) extensions.Add(".srt");
            if (request.OutputVtt) extensions.Add(".vtt");
            foreach (var extension in extensions)
            {
                var target = request.OutputBasePath + extension;
                if (File.Exists(target) || Directory.Exists(target))
                    throw new IOException($"出力先が既に存在します: {target}。別の出力名で再実行してください。");
            }
            Directory.CreateDirectory(stagingDirectory);
            var stagedOutput = Path.Combine(stagingDirectory, "result");
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
                "-of", stagedOutput
            };
            if (request.OutputTxt) args.Add("-otxt");
            if (request.OutputSrt) args.Add("-osrt");
            if (request.OutputVtt) args.Add("-ovtt");

            args.AddRange(stableArgs);
            try
            {
                await RunProcessAsync(request.WhisperPath, args, log, cancellationToken);
            }
            catch (InvalidOperationException ex) when (stableArgs.Contains("--vad"))
            {
                throw new InvalidOperationException(ex.Message + " VAD使用中に失敗しました。処理ログを確認し、モデル破損の場合はsetup-vad.ps1 -Forceで再配置してください。", ex);
            }
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var extension in extensions)
            {
                if (!File.Exists(stagedOutput + extension))
                    throw new IOException($"結果ファイルが生成されませんでした: {extension}。処理ログを確認してください。");
            }
            // Empty transcripts are valid for silence. Never overwrite an existing result.
            foreach (var extension in extensions)
                File.Move(stagedOutput + extension, request.OutputBasePath + extension, overwrite: false);
            log.Report("文字起こしが完了しました。");
        }
        catch (OperationCanceledException)
        {
            log.Report("処理を中止しました。");
            throw;
        }
        catch (Exception ex)
        {
            log.Report($"エラー: {ex.Message}");
            throw;
        }
        finally
        {
            TryDelete(temporaryWav, log);
            try
            {
                if (Directory.Exists(stagingDirectory)) Directory.Delete(stagingDirectory, recursive: true);
            }
            catch (IOException ex) { log.Report($"一時出力の削除に失敗しました: {stagingDirectory}: {ex.Message}"); }
            catch (UnauthorizedAccessException ex) { log.Report($"一時出力の削除に失敗しました: {stagingDirectory}: {ex.Message}"); }
            _activeProcess = null;
            elapsed.Stop();
            log.Report($"総処理時間（変換・保存を含む）: {elapsed.Elapsed}");
        }
    }

    private static async Task<string> ReadHelpAsync(string executable, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add("--help");
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var registration = timeout.Token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        });
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            // Some CLI versions return a nonzero exit code for --help.
            return await stdout + "\n" + await stderr;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("whisper-cliの機能確認がタイムアウトしました。実行環境を確認してください。");
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
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

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            // Cancellation stops waiting, not the OS process. Reap it before deleting WAVs.
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            _activeProcess = null;
        }
        cancellationToken.ThrowIfCancellationRequested();
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

    private static void TryDelete(string path, IProgress<string> log)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException ex) { log.Report($"一時WAVの削除に失敗しました: {path}: {ex.Message}"); }
        catch (UnauthorizedAccessException ex) { log.Report($"一時WAVの削除に失敗しました: {path}: {ex.Message}"); }
    }
}
