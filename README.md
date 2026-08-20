# WhisperDesk

Windows上のVulkan対応`whisper.cpp`をGUIから利用する、ローカル文字起こしツールです。

## 前提

- Windows 11
- .NET 8 SDK（ビルド時のみ）
- Vulkan対応でビルド済みの`whisper-cli.exe`
- Whisper GGMLモデル
- FFmpeg

既定では次の既存環境を参照します。

```text
C:\WhisperTool\whisper.cpp\build\bin\Release\whisper-cli.exe
C:\WhisperTool\models\
```

## 1. FFmpeg

PowerShellで次を実行します。

```powershell
.\setup-ffmpeg.ps1
```

既にWindows側で`ffmpeg.exe`がPATHに入っていれば、変更は行いません。

## 2. ビルド

このフォルダーでPowerShellを開きます。スクリプトの実行を許可していない環境では、このプロセスだけ許可します。

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build.ps1
```

完成したアプリは次の場所です。

```text
publish\WhisperDesk.exe
```

自己完結型で発行するため、実行するPCに.NETランタイムは不要です。

## 3. 使用方法

1. `WhisperDesk.exe`を起動します。
2. 音声または動画をウィンドウへドロップします。
3. 出力先、モデル、言語、出力形式を選択します。
4. 「文字起こしを開始」を押します。

入力はFFmpegで一時的な16kHz・モノラルWAVへ変換されます。一時ファイルは処理後に削除されます。同名の結果がある場合は日時を付け、既存ファイルを上書きしません。

## モデルの基本的な使い分け

| モデル | 用途 |
|---|---|
| `ggml-small.bin` | 速度優先 |
| `ggml-medium-q5_0.bin` | 通常使用。速度と精度の基本線 |
| `ggml-large-v3-q5_0.bin` | 精度優先。処理時間とメモリ消費は増加 |

## 設定保存先

```text
%APPDATA%\WhisperDesk\settings.json
```

## 初版の制約

- 一度に処理するファイルは1本です。
- 話者分離、段落再構成、LLMによる要約・記事化は行いません。
- 進捗バーは処理中を示す形式で、完了率の数値表示はまだありません。
