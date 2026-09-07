namespace WhisperDesk.Models;

public sealed class AppSettings
{
    public string WhisperPath { get; set; } = @"C:\WhisperTool\whisper.cpp\build\bin\Release\whisper-cli.exe";
    public string FfmpegPath { get; set; } = "ffmpeg.exe";
    public string ModelDirectory { get; set; } = @"C:\WhisperTool\models";
    public string OutputDirectory { get; set; } = "";
    public string SelectedModel { get; set; } = "ggml-medium-q5_0.bin";
    public string Language { get; set; } = "ja";
    public bool StableMode { get; set; }
    public bool OutputTxt { get; set; } = true;
    public bool OutputSrt { get; set; }
    public bool OutputVtt { get; set; }
}
