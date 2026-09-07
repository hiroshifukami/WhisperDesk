using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WhisperDesk.Models;
using WhisperDesk.Services;

namespace WhisperDesk;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly TranscriptionService _transcriptionService = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private AppSettings _settings;
    private CancellationTokenSource? _cancellation;
    private Stopwatch? _stopwatch;
    private TaskCompletionSource? _runCompletion;
    private bool _closingAfterRun;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        ApplySettings();
        RefreshModels();
        _timer.Tick += (_, _) => ElapsedText.Text = (_stopwatch?.Elapsed ?? TimeSpan.Zero).ToString(@"hh\:mm\:ss");
    }

    private void ApplySettings()
    {
        WhisperPathBox.Text = _settings.WhisperPath;
        FfmpegPathBox.Text = _settings.FfmpegPath;
        ModelDirectoryBox.Text = _settings.ModelDirectory;
        OutputDirectoryBox.Text = _settings.OutputDirectory;
        TxtCheck.IsChecked = _settings.OutputTxt;
        SrtCheck.IsChecked = _settings.OutputSrt;
        VttCheck.IsChecked = _settings.OutputVtt;
        StableModeCheck.IsChecked = _settings.StableMode;
        SelectLanguage(_settings.Language);
    }

    private void RefreshModels()
    {
        var selected = ModelCombo.SelectedItem as string ?? _settings.SelectedModel;
        ModelCombo.Items.Clear();
        if (Directory.Exists(ModelDirectoryBox.Text))
        {
            foreach (var path in Directory.GetFiles(ModelDirectoryBox.Text, "ggml-*.bin").OrderBy(Path.GetFileName))
                if (!Path.GetFileName(path).StartsWith("ggml-silero-", StringComparison.OrdinalIgnoreCase))
                    ModelCombo.Items.Add(Path.GetFileName(path));
        }
        ModelCombo.SelectedItem = ModelCombo.Items.Contains(selected) ? selected : ModelCombo.Items.Cast<string>().FirstOrDefault();
    }

    private void BrowseInput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "音声・動画|*.wav;*.mp3;*.m4a;*.aac;*.flac;*.ogg;*.wma;*.mp4;*.mov;*.mkv;*.webm|すべてのファイル|*.*"
        };
        if (dialog.ShowDialog() == true) SetInput(dialog.FileName);
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "文字起こし結果の保存先を選択" };
        if (dialog.ShowDialog() == true) OutputDirectoryBox.Text = dialog.FolderName;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (_runCompletion is not null) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            SetInput(files[0]);
    }

    private void SetInput(string path)
    {
        InputPathBox.Text = path;
        if (string.IsNullOrWhiteSpace(OutputDirectoryBox.Text))
            OutputDirectoryBox.Text = Path.GetDirectoryName(path) ?? "";
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_runCompletion is not null) return;
        _runCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var request = BuildRequest();
            SaveSettings();
            SetRunning(true);
            LogBox.Clear();
            AppendLog($"入力: {request.InputPath}");
            AppendLog($"モデル: {request.ModelPath}");
            AppendLog($"出力: {request.OutputBasePath}");

            _cancellation = new CancellationTokenSource();
            _stopwatch = Stopwatch.StartNew();
            _timer.Start();
            var progress = new Progress<string>(AppendLog);
            await _transcriptionService.RunAsync(request, progress, _cancellation.Token);
            StopClock();
            if (!_closingAfterRun)
                MessageBox.Show(this, "文字起こしが完了しました。", "WhisperDesk", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog("処理を中止しました。");
        }
        catch (Exception ex)
        {
            StopClock();
            AppendLog($"エラー: {ex.Message}");
            if (!_closingAfterRun)
                MessageBox.Show(this, ex.Message, "WhisperDesk", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            StopClock();
            _cancellation?.Dispose();
            _cancellation = null;
            SetRunning(false);
            _runCompletion.TrySetResult();
            _runCompletion = null;
        }
    }

    private TranscriptionRequest BuildRequest()
    {
        var input = InputPathBox.Text.Trim();
        if (!File.Exists(input)) throw new InvalidOperationException("入力ファイルを選択してください。");
        var outputDirectory = OutputDirectoryBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputDirectory)) throw new InvalidOperationException("出力フォルダーを選択してください。");
        var model = ModelCombo.SelectedItem as string ?? throw new InvalidOperationException("モデルを選択してください。");
        var language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ja";
        var outputBase = CreateUniqueOutputBase(outputDirectory, Path.GetFileNameWithoutExtension(input));

        return new TranscriptionRequest(input, outputBase, WhisperPathBox.Text.Trim(), FfmpegPathBox.Text.Trim(),
            Path.Combine(ModelDirectoryBox.Text.Trim(), model), language,
            TxtCheck.IsChecked == true, SrtCheck.IsChecked == true, VttCheck.IsChecked == true, StableModeCheck.IsChecked == true);
    }

    private string CreateUniqueOutputBase(string directory, string inputName)
    {
        var basePath = Path.Combine(directory, inputName + "_transcript");
        var extensions = new[] { ".txt", ".srt", ".vtt", ".log" };
        var candidate = basePath;
        while (extensions.Any(ext => File.Exists(candidate + ext) || Directory.Exists(candidate + ext)))
            candidate = basePath + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..8];
        return candidate;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        AppendLog("中止を要求しています…");
        _cancellation?.Cancel();
        _transcriptionService.Cancel();
    }

    private void SetRunning(bool running)
    {
        InputPanel.IsEnabled = !running;
        OutputPanel.IsEnabled = !running;
        RecognitionPanel.IsEnabled = !running;
        EnvironmentPanel.IsEnabled = !running;
        AllowDrop = !running;
        StableModeCheck.IsEnabled = !running;
        StartButton.IsEnabled = !running;
        CancelButton.IsEnabled = running;
        ProgressBar.IsIndeterminate = running;
    }

    private void StopClock()
    {
        _timer.Stop();
        _stopwatch?.Stop();
        ElapsedText.Text = (_stopwatch?.Elapsed ?? TimeSpan.Zero).ToString(@"hh\:mm\:ss");
    }

    private void AppendLog(string line)
    {
        LogBox.AppendText(line + Environment.NewLine);
        LogBox.ScrollToEnd();
    }

    private void ModelDirectoryBox_LostFocus(object sender, RoutedEventArgs e) => RefreshModels();

    private void SelectLanguage(string language)
    {
        foreach (ComboBoxItem item in LanguageCombo.Items)
            if (string.Equals(item.Tag?.ToString(), language, StringComparison.OrdinalIgnoreCase))
                LanguageCombo.SelectedItem = item;
    }

    private void SaveSettings()
    {
        _settings = new AppSettings
        {
            WhisperPath = WhisperPathBox.Text.Trim(),
            FfmpegPath = FfmpegPathBox.Text.Trim(),
            ModelDirectory = ModelDirectoryBox.Text.Trim(),
            OutputDirectory = OutputDirectoryBox.Text.Trim(),
            SelectedModel = ModelCombo.SelectedItem as string ?? "",
            Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ja",
            OutputTxt = TxtCheck.IsChecked == true,
            OutputSrt = SrtCheck.IsChecked == true,
            StableMode = StableModeCheck.IsChecked == true,
            OutputVtt = VttCheck.IsChecked == true
        };
        _settingsService.Save(_settings);
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_allowClose) { base.OnClosing(e); return; }
        if (_runCompletion is not null)
        {
            e.Cancel = true;
            if (_closingAfterRun) return;
            var completion = _runCompletion.Task;
            if (MessageBox.Show(this, "処理中です。中止して終了しますか？", "WhisperDesk",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            _closingAfterRun = true;
            _cancellation?.Cancel();
            _transcriptionService.Cancel();
            await completion;
            _allowClose = true;
            // Queue a fresh close event even when completion was already finished.
            Dispatcher.BeginInvoke(new Action(Close));
            return;
        }
        else
        {
            try { SaveSettings(); } catch { }
        }
        base.OnClosing(e);
    }
}
