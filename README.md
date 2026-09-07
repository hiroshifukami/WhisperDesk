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

## 安定モード（反復を抑制）

「安定モード」をONにすると、前の認識結果を次の区間へ引き継がなくなります。
既定はOFFで、以前の設定ファイルもそのまま使用できます。OFF時の文字起こしコマンドは従来通りです。
ON/OFFは設定に保存されます。固有名詞の統一や文章の連続性が弱くなる場合があり、反復の完全防止を保証するものではありません。

- CLIの`--help`で機能を確認し、対応していれば`--no-context`を使用します。
- `--no-context`のないCLIでは`--max-context 0`を使用します。現在のローカルCLIはこちらです。
- どちらにも未対応の場合は開始前にエラーを表示します。機能確認は15秒でタイムアウトし、中止も可能です。
- temperature fallback、beam search、GPU・スレッド数は既存のCLI既定値を維持します。

VADを利用する場合、アプリのモデルフォルダーへSilero VADモデルを配置します。
WhisperDeskフォルダーで一度だけ実行してください（ダウンロード時はインターネット接続が必要です）。

```powershell
.\setup-vad.ps1
# モデルフォルダーを変更している場合
.\setup-vad.ps1 -ModelDirectory 'D:\Models'
```

`ggml-silero-v6.2.0.bin`を既定では`C:\WhisperTool\models`に保存します。
取得元はwhisper.cpp公式ダウンロードスクリプトと同じ`ggml-org/whisper-vad`です。
一時ファイルへのダウンロードと形式確認が成功してから配置します。
既存ファイルは通常上書きせず、破損時は`-Force`で再取得できます。
ビルド手順は変更ありません。VADモデルはexeには埋め込みません。

安定モードON、CLIがVAD対応、モデル配置済みの3条件が揃えば、`--vad --vad-model <パス>`を自動追加します。
VADモデルは文字起こしモデルの選択肢から除外します。未配置・CLI未対応の場合はログで理由を表示し、文脈引き継ぎ抑制だけで続行します。
モデルの読み取り・形式エラーは変換前に停止します。推論中のエラーも画面とログに表示し、VADなしでの自動再実行はしません。
形式確認はヘッダー検査であり、全テンソルの完全性確認はCLIの読み込み時に行われます。

## 設定保存先

```text
%APPDATA%\WhisperDesk\settings.json
```

## 初版の制約

- 一度に処理するファイルは1本です。
- 話者分離、段落再構成、LLMによる要約・記事化は行いません。
- 進捗バーは処理中を示す形式で、完了率の数値表示はまだありません。
