# 同梱コンポーネントと外部依存

WhisperDesk本体はMIT Licenseです。全文は`LICENSE`を参照してください。

Windows x64の自己完結型exeには.NETランタイムが含まれます。
v0.1.0のビルドで使用したパッケージは以下の通りです。

| パッケージ | バージョン | 配布物から取得した通知 |
|---|---|---|
| Microsoft.NETCore.App.Runtime.win-x64 | 8.0.30 | `licenses/dotnet-runtime-LICENSE.txt`、`licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt` |
| Microsoft.WindowsDesktop.App.Runtime.win-x64 | 8.0.30 | `licenses/dotnet-windowsdesktop-LICENSE.txt` |

通知文はビルドに使用したNuGetパッケージからそのままコピーしています。ランタイムの版を更新して再配布する場合は、通知も更新してください。

以下は本ZIPに含まれず、利用者が別途取得する外部依存です。

- [whisper.cpp](https://github.com/ggml-org/whisper.cpp)：音声認識エンジン。
- [FFmpeg](https://ffmpeg.org/)：音声変換。入手するビルドごとの配布条件を確認してください。
- Whisper GGMLモデル、Silero VADモデル：各配布元のライセンスを確認してください。

`setup-vad.ps1`は実行時に[ggml-org/whisper-vad](https://huggingface.co/ggml-org/whisper-vad)へ接続してモデルを取得します。
外部ソフト・モデルにWhisperDeskのMIT Licenseを適用するものではありません。
