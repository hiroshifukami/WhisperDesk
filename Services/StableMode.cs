using System.Text;
using System.Text.RegularExpressions;

namespace WhisperDesk.Services;

public static class StableMode
{
    public const string VadModelFileName = "ggml-silero-v6.2.0.bin";

    public static IReadOnlyList<string> CreateArguments(string help, string modelDirectory, IProgress<string> log)
    {
        bool Supports(string option) => Regex.IsMatch(help,
            @"(?<![\w-])" + Regex.Escape(option) + @"(?=\s|,|$)");
        var args = new List<string>();
        if (Supports("--no-context")) args.Add("--no-context");
        else if (Supports("--max-context")) args.AddRange(["--max-context", "0"]);
        else throw new InvalidOperationException("このwhisper-cliは安定モードに対応していません。CLIを更新するか安定モードをOFFにしてください。");

        log.Report("安定モード: 前の認識結果の引き継ぎを無効にします。");
        if (!Supports("--vad") || !Supports("--vad-model"))
        {
            log.Report("VAD未対応のCLIです。文脈引き継ぎ抑制のみで続行します。");
            return args;
        }

        var path = Path.GetFullPath(Path.Combine(modelDirectory, VadModelFileName));
        if (!File.Exists(path))
        {
            log.Report($"VADモデル未配置: {path}。setup-vad.ps1で配置できます。今回は文脈引き継ぎ抑制のみで続行します。");
            return args;
        }

        try
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.BaseStream.Length < 1024 || reader.ReadUInt32() != 0x67676d6c ||
                reader.ReadInt32() != 10 || Encoding.ASCII.GetString(reader.ReadBytes(10)) != "silero-16k")
                throw new InvalidDataException("Silero VADモデルの形式が不正です。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new InvalidOperationException($"VADモデルを読み込めません: {path}。setup-vad.ps1 -Forceで再配置するか、安定モードをOFFにしてください。", ex);
        }
        args.AddRange(["--vad", "--vad-model", path]);
        log.Report($"VADを有効にします: {path}");
        return args;
    }
}
