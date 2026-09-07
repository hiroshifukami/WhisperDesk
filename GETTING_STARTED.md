# WhisperDesk v0.1.0 導入ガイド

Windows用のローカル文字起こしGUIです。whisper.cppとFFmpegを自分で準備できる方を対象にしています。
本配布はWindows x64向けです。macOS・Linux・Windows ARM64用ではありません。

## 1. 配布ZIPを展開する

[Releases](https://github.com/hiroshifukami/WhisperDesk/releases)から
`WhisperDesk-v0.1.0-win-x64.zip`をダウンロードし、書き込み可能なフォルダーへ展開してください。
`Source code (zip)`は開発用ソースで、exeの配布ZIPとは別です。
WhisperDesk自体は.NETランタイムを含むため、利用者が.NET SDKを入れる必要はありません。

## 2. 外部プログラムとモデルを準備する

| 必要なもの | 入手先・条件 |
|---|---|
| whisper-cli.exe | [whisper.cpp公式リポジトリ](https://github.com/ggml-org/whisper.cpp)のビルド手順、または[公式リリース](https://github.com/ggml-org/whisper.cpp/releases)を参照。Windows x64用を用意し、付属DLLも同じフォルダーに保持してください。 |
| FFmpeg | [FFmpeg公式ダウンロード案内](https://ffmpeg.org/download.html)からWindows向けビルドを入手。PATHを設定するか、GUIにffmpeg.exeのフルパスを指定します。 |
| Whisper GGMLモデル | [whisper.cppのモデル案内](https://github.com/ggml-org/whisper.cpp/tree/master/models)を参照。日本語には`.en`付きの英語専用モデルではなく、多言語モデルを使います。 |
| Silero VADモデル（推奨） | 手順4のスクリプトで取得できます。認識モデルとは別の小さなモデルです。 |

Vulkan版を使う場合は対応GPUとドライバーが必要です。まず、そのCLIが単体で実行できることを確認してください。
CPU版や他のバックエンドはCLIの仕様上利用可能な構成ですが、本リリースでの動作確認対象はVulkan版です。

開発者の確認環境はWindows 11 x64、AMD Radeon(TM) Graphics（Vulkan）、whisper.cpp `1.9.3-dev`です。
`ggml-small.bin`と`ggml-medium-q5_0.bin`を使用しました。全GPU・全CLIバージョンでの動作を保証するものではありません。
メモリ消費はモデルとバックエンドに依存します。最初はsmallなどの小さいモデルと短い音源で確認してください。
一時WAVは16kHz・モノラル・16bitで、音声1時間あたり約115MB（約110MiB）のディスク領域を使います。

## 3. GUIでパスを設定する

`WhisperDesk.exe`を起動し、「実行環境の設定」を展開してください。

- **whisper-cli**：準備した`whisper-cli.exe`のフルパス。
- **FFmpeg**：`ffmpeg.exe`のフルパス。PATH設定済みなら既定の`ffmpeg.exe`でも構いません。
- **モデルフォルダー**：`ggml-*.bin`を置いたフォルダー。

初期値の`C:\WhisperTool\...`は配置例です。別の場所に配置した場合は必ず変更してください。
モデルフォルダーを入力して別の欄をクリックすると、モデル一覧が更新されます。

例：

```text
C:\WhisperTool\
  WhisperDesk\WhisperDesk.exe
  whisper.cpp\build\bin\Release\whisper-cli.exe  （付属DLLも保持）
  ffmpeg\bin\ffmpeg.exe
  models\ggml-small.bin
```

## 4. VADを準備する

ZIPを展開したフォルダーでPowerShellを開き、実際のモデルフォルダーを指定して実行します。

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\setup-vad.ps1 -ModelDirectory 'C:\WhisperTool\models'
```

公式配布元から`ggml-silero-v6.2.0.bin`をダウンロードします。初回取得には通信が必要です。
破損したモデルを再取得する場合は`-Force`を追加してください。
FFmpegの導入にwingetを使いたい場合は、同梱の`setup-ffmpeg.ps1`も利用できます。

## 5. 短い音源で試す

1. 音声・動画をドロップするか選択します。
2. 出力フォルダー、モデル、日本語または自動判定、TXT/SRT/VTTを選びます。
3. 反復が気になる音源では「安定モード」をONにします。初期値はOFFです。
4. 「文字起こしを開始」を押します。
5. 結果と、同じ名前の`.log`が保存されたことを確認します。

安定モードは直前の認識結果の引き継ぎを止めます。CLIの対応に応じて`--no-context`または`--max-context 0`を使い、CLIがVAD対応でモデルも配置されていればVADを追加します。
VADなしで続行する場合はログに理由を表示します。固有名詞の連続性や小声の拾い方が変わる場合があるため、結果を元音声と照合してください。
音源はローカルの外部プログラムに渡され、WhisperDeskからクラウドの文字起こしAPIへ送信する処理はありません。

## 困ったとき

| 症状 | 確認すること |
|---|---|
| 起動時にWindowsの警告が出る | この初回exeはコード署名していません。配布元とSHA-256を確認し、組織の制限がある場合は管理者に相談してください。 |
| CLIが見つからない・起動しない | フルパス、付属DLL、使用するCLIの必要ランタイムとGPUドライバーを確認します。 |
| モデルが一覧に出ない | フォルダーと`ggml-*.bin`形式を確認します。Sileroモデルは一覧に出ません。 |
| 安定モードの機能確認でエラー | `whisper-cli.exe --help`が単体で動くか、対応オプションがあるか確認します。 |
| 保存エラー | 出力先の書き込み権限と空き容量を確認します。既存の結果やログは上書きしません。 |
| 認識の誤り・抜け | 元音声との比較、小さい区間での再確認、別モデルでの比較を行います。反復の完全防止は保証しません。 |

設定は`%APPDATA%\WhisperDesk\settings.json`に保存されます。exeを置き換えても引き継がれます。
不具合は[Issues](https://github.com/hiroshifukami/WhisperDesk/issues)へ、OS、CLI版、モデル名、再現手順を添えて報告してください。
ログには本文やファイルパスが含まれます。公開する前に不要な個人情報を取り除いてください。

## 配布物とライセンス

WhisperDesk本体はMITライセンスです（`LICENSE`）。.NETの著作権・第三者通知は`THIRD_PARTY_NOTICES.md`と`licenses/`を参照してください。
whisper.cpp、FFmpeg、Whisper/SileroのモデルはこのZIPに含まれません。利用者がそれぞれの配布条件に従って入手してください。
