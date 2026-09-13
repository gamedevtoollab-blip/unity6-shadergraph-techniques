# 撮影手順と媒体台帳

## 実際に使った撮影方法

`Artifacts/Player/Windows/ShaderGraphTechniques.exe`を可視ウィンドウで起動し、`PlayerCaptureSequence`で実UnityフレームをPNG連番へ保存しました。Sceneは`-captureScene`で指定できるため、Build Settingsの先頭を変更する必要はありません。

```powershell
$output = '<project>/Artifacts/Captures/Showcase/<run>/Frames/01_Dissolve'
./Artifacts/Player/Windows/ShaderGraphTechniques.exe `
  -captureSequence `
  -captureScene 01_Dissolve `
  -captureOutput $output `
  -captureFrames 600 `
  -captureFps 60 `
  -captureWidth 1920 `
  -captureHeight 1080 `
  -captureHideUi `
  -showcaseMotion `
  -screen-fullscreen 0
```

処理はScene読込完了を待ち、解像度を設定し、1フレーム描画を待ってから、Demo時計を0秒へResetします。Showcase撮影では各フレームの時刻を`frame / fps`として`DemoPresentationController.SetManualTime`へ明示的に渡し、`CaptureScreenshotAsTexture`で同期取得します。これによりShader時間、Dissolve、Scan、草Interactor、対象物を同じ固定刻みで評価し、非同期スクリーンショットの重複を避けています。カメラはScene作者の位置・回転・画角へ毎フレーム戻し、各連番の`camera-trace.csv`へ記録します。UIとカーソルは非表示です。

ScreenCaptureは非表示ウィンドウでは黒くなる環境だったため、最終媒体はすべて可視Playerから撮り直しました。非表示起動で生成した`03_HeatHaze-Perspective-final`、`02_IntersectionShield-Orthographic`、`03_HeatHaze-Orthographic`は不採用で、下表と検証マニフェストには含めていません。

Orthographicは同じPlayerに`-orthographic -orthographicSize <size>`を追加して02と03を確認しました。Perspectiveを基本対応とし、Orthographicはこの2作例で確認した範囲だけを対応実績とします。

## エンコードと検査

PNG連番を`Tools/Encode-Capture.ps1`相当のffmpeg設定（H.264、CRF 18、`yuv420p`、faststart）でMP4へ変換しました。`Tools/Validate-Media.ps1`が入力フレーム数、`capture-complete.txt`、Playerログ、codec、解像度、pixel format、fps、出力フレーム数、長さ、SHA-256を確認し、9/9 PASSしています。

これは固定時間刻みのオフライン生成です。動画が60fpsストリームであることは確認しましたが、実時間で60fps描画できた性能証拠ではありません。

## YouTube向け単一映像

公開用候補は次の1ファイルです。

| 出力MP4 | フレーム | 長さ | 映像仕様 | 音声 | SHA-256 |
|---|---:|---:|---|---|---|
| `ShaderGraphTechniques-YouTube-Showcase-Final.mp4` | 3,900 | 65.000秒 | 1920×1080、60fps、H.264 High、`yuv420p` | AAC LC、48kHz、ステレオ、約209kbps | `7a837ad5f17dc15b5f0210a70481a4910a16ab90c7016b7b45c9b09d288d8e73` |

構成はタイトル3秒、6作例を各10秒、アウトロ2秒です。`1:04`から終了までの最後の1秒は、YouTube用サムネイルを全画面表示します。章時刻は次のとおりです。

| 時刻 | 内容 |
|---:|---|
| `0:00` | タイトル |
| `0:03` | 01 Dissolve |
| `0:13` | 02 Intersection Shield |
| `0:23` | 03 Heat Haze |
| `0:33` | 04 Triplanar Snow |
| `0:43` | 05 Interactive Grass |
| `0:53` | 06 Scan Pulse |
| `1:03` | アウトロ |
| `1:04` | サムネイル終端カード |

`Tools/Capture-Showcase.ps1`で素材を撮影し、`Tools/Build-ShowcaseVideo.ps1`で章カード、左上の章名、進捗バー、フェード、6本の日本語効果説明字幕、BGMを合成しました。採用連番は`Artifacts/Captures/Showcase/YouTubeShowcase-v6-clean-shield/`です。`Tools/Validate-CameraLock.ps1`により、Galleryを含む7本すべてで、`camera-trace.csv`の全3,780行が各Sceneにつき同一の位置・回転・画角であることを確認しました。02は比較用の`Side Pillar`と`Occlusion Check`をShowcase中だけ非表示にし、拡大したバリアへ唯一の柱が外側から入り、交差状態で停止して反対側へ抜ける演出です。`Tools/Validate-ShowcaseVideo.ps1`は全編デコード、codec、解像度、pixel format、fps、3,900フレーム、65秒、70秒以内に加え、AAC音声1本、48kHzステレオ、65秒、ラウドネス、True Peakを確認します。さらに各作例の1／3／5／7／9秒を抽出し、30/30点が別ハッシュであることと、モーション接触シートを検査します。最後に、検証済み映像の`64.000`秒から`65.000`秒へ1920×1080に縮小したサムネイルを合成し、総尺とAAC音声を維持しました。境界直前、切替後、終了直前のフレーム、および映像・音声がともに65.000秒であることを再確認しています。結果は`Artifacts/Validation/Showcase/camera-lock-validation.json`と`showcase-video-validation.json`です。

BGMはMixkitの`Digital Clouds`（Alejandro Magaña (A. M.)）です。原音源の冒頭65秒を使い、0.8秒フェードイン、終端3秒フェードアウト、`-16 LUFS`／True Peak `-1.5 dBFS`目標で処理しました。最終MP4の実測は`-15.7 LUFS`、True Peak `-1.1 dBFS`です。出典、ライセンス、原音源ハッシュ、任意クレジット文は`Documentation/MUSIC.md`にあります。

## 個別技術確認媒体台帳

すべて`1920×1080`、`60 fps`、H.264／`yuv420p`です。フレーム番号は両端を含みます。

| 媒体 | 出力MP4 | フレーム | 長さ | SHA-256 |
|---|---|---:|---:|---|
| Gallery Perspective | `00_Gallery-Perspective.mp4` | `0000–0179`（180） | 3.000秒 | `b718fdc68b1faf427914f60dec1e845dfd5f15f56f8ef62411980b3d18591d29` |
| Dissolve Perspective | `01_Dissolve-Perspective.mp4` | `0000–0179`（180） | 3.000秒 | `fa4a4adc95b7c2a48faf52517bcb702ce0a9dc8dad6aaf629d1e19a98f88d101` |
| Intersection Shield Perspective | `02_IntersectionShield-Perspective.mp4` | `0000–0179`（180） | 3.000秒 | `d3f6e8a9901530911fa47c5eb8254938322e003c049da73c6eabaa17829466fe` |
| Heat Haze Perspective | `03_HeatHaze-Perspective.mp4` | `0000–0179`（180） | 3.000秒 | `433dda1745225dad8380f4f85ce3d9146279df67a70152a546821b95d4656845` |
| Triplanar Snow Perspective | `04_TriplanarSnow-Perspective.mp4` | `0000–0179`（180） | 3.000秒 | `46ff4cb3d1322ea0e4ed19a030765648f02ff28dcf3dfea397de6ab3e3c17028` |
| Interactive Grass Perspective | `05_InteractiveGrass-Perspective.mp4` | `0000–0179`（180） | 3.000秒 | `17074dc05430b250f81b29739d1d21de39aa1d03e392b7652981332e7163aacb` |
| Scan Pulse Perspective | `06_ScanPulse-Perspective.mp4` | `0000–0179`（180） | 3.000秒 | `825b65636c8d2ff3697c9f4cfae0b10012432e5cebce90beb6f67b674b58d90a` |
| Intersection Shield Orthographic | `02_IntersectionShield-Orthographic.mp4` | `0000–0119`（120） | 2.000秒 | `fe1c1246cc9f14fe7796014a2b8d6703fba2ea02c5359fe1060f9042509a4eee` |
| Heat Haze Orthographic | `03_HeatHaze-Orthographic.mp4` | `0000–0119`（120） | 2.000秒 | `42dbcaa9ab2d68061144f6a1bd3fa9a51dd02cdf969039fcd23f7604071d49e3` |

単一映像と個別MP4は`Artifacts/Captures/Videos/`、Showcaseの採用連番は`Artifacts/Captures/Showcase/YouTubeShowcase-v6-clean-shield/`、旧個別連番は`Artifacts/Captures/Frames/`にあります。個別9本の完全な入力／出力ハッシュは`Artifacts/Validation/media-validation.json`です。単一映像のナビゲーション接触シート、30点モーション接触シート、6本の字幕切り出し、02の交差中フレームも目視確認しています。

YouTubeへのアップロード、公開URL、公開動画の開始時刻付きリンク、Shader Graph Editor画面の公開用スクリーンショットは未生成です。
