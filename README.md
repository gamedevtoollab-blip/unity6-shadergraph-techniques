# Unity 6.3 Shader Graph — Six Reusable Techniques

Repository: https://github.com/gamedevtoollab-blip/unity6-shadergraph-techniques

## Quick Start (English)

1. Clone this repository: `git clone https://github.com/gamedevtoollab-blip/unity6-shadergraph-techniques.git`.
2. Open the cloned folder with Unity `6000.3.22f1`.
3. Open `Assets/ShaderGraphTechniques/Demo/Scenes/00_Gallery.unity` and press Play.
4. To reuse one effect, copy its folder under `Assets/ShaderGraphTechniques/Runtime/` plus the dependencies named in that folder's `GETTING_STARTED_JA.md`. To create importable packages locally, run `Tools > Shader Graph Techniques > Export Runtime Packages` in Unity.
5. Keep URP Depth Texture enabled for Intersection Shield and Opaque Texture enabled for Heat Haze.

The six effects are real editable Shader Graph 17.3 assets. Runtime prefabs contain no camera, light, volume, UI, or presentation manager.

Validated locally on 2026-09-12 with Unity `6000.3.22f1`, URP / Shader Graph `17.3.0`: project checks `68/68`, EditMode `9/9`, PlayMode `4/4`, Windows StrictMode Player build `0 errors / 0 warnings`, package archive checks `7/7`, and isolated imports `7/7`. A fresh GitHub checkout was reimported with the same Editor on 2026-09-13 and independently passed EditMode `9/9` and GPU-backed PlayMode `4/4`.

Generated players, packages, migration projects, validation evidence, captures, and videos are written under the git-ignored `Artifacts/` directory and are not part of the source checkout.

The local single-file showcase is `Artifacts/Captures/Videos/ShaderGraphTechniques-YouTube-Showcase-Final.mp4`: 65.000 seconds, 1920×1080, 60 fps, H.264, with a 3-second opener, six 10-second effects, and a 2-second outro whose final second displays the video thumbnail. The camera stays locked while the effects and subjects move, and every chapter includes a Japanese explanatory caption. The Intersection Shield chapter hides its two static comparison objects so only the shield and moving pillar remain. The soundtrack is `Digital Clouds` by Alejandro Magaña (A. M.), used under the Mixkit Stock Music Free License; source and license details are in `Documentation/MUSIC.md`.

## 日本語クイックスタート

このプロジェクトは、記事「Unity 6.3 Shader Graphで作る、ゲームに持ち込みたくなる表現6選」の実装サンプルです。Unity `6000.3.22f1`、URP／Shader Graph `17.3.0`、Linear、Universal RendererのForward経路を固定対象にしています。

`Assets/ShaderGraphTechniques/Demo/Scenes/00_Gallery.unity`を開いてPlayすると、6作例を同じ展示空間で確認できます。`01_`〜`06_`の個別Sceneでは観察対象を大きく表示します。画面左上のFreeze／Resume、Reset、Hide UIはDemo専用であり、Runtime Prefabの利用には不要です。Player、Package、動画、検証ログなどの生成物はGit管理外の`Artifacts/`へ出力されます。

## 対応表

| # | 作例 | Graph | Runtime Prefab | 個別Scene |
| --- | --- | --- | --- | --- |
| 01 | Dissolve | `Runtime/01_Dissolve/SG_01_Dissolve.shadergraph` | `PF_01_Dissolve.prefab` | `Demo/Scenes/01_Dissolve.unity` |
| 02 | Intersection Shield | `Runtime/02_IntersectionShield/SG_02_IntersectionShield.shadergraph` | `PF_02_IntersectionShield.prefab` | `Demo/Scenes/02_IntersectionShield.unity` |
| 03 | Heat Haze | `Runtime/03_HeatHaze/SG_03_HeatHaze.shadergraph` | `PF_03_HeatHaze.prefab` | `Demo/Scenes/03_HeatHaze.unity` |
| 04 | Triplanar Snow | `Runtime/04_TriplanarSnow/SG_04_TriplanarSnow.shadergraph` | `PF_04_TriplanarSnow.prefab` | `Demo/Scenes/04_TriplanarSnow.unity` |
| 05 | Interactive Grass | `Runtime/05_InteractiveGrass/SG_05_InteractiveGrass.shadergraph` | `PF_05_InteractiveGrass.prefab` | `Demo/Scenes/05_InteractiveGrass.unity` |
| 06 | Scan Pulse | `Runtime/06_ScanPulse/SG_06_ScanPulse.shadergraph` | `PF_06_ScanPulse.prefab` | `Demo/Scenes/06_ScanPulse.unity` |

## 設計境界

- 依存方向は`Demo → Runtime`です。RuntimeはDemo、Editor、Tests、Recorder、UI、記事用ファイルを参照しません。
- 個別制御する作例は`MaterialInstanceOwner`がマテリアルを一度だけ複製し、再利用し、無効化時に元参照を戻して自身の複製だけを破棄します。共有Materialアセットや`Shader.SetGlobal*`は変更しません。
- RuntimeコンポーネントはScene名、GameObject名、Tag／Layer、`Resources`、`Camera.main`、Scene起動順を要求しません。
- 動画や配布物は`Artifacts/`に生成し、ソース用Assetsとは分離します。

## 検証済み成果物

- Windows Player: `Artifacts/Player/Windows/ShaderGraphTechniques.exe`。Gallery＋6個別Sceneを含み、実起動してキャプチャまで確認済みです。
- 配布物: `Artifacts/Packages/`の6作例別`.unitypackage`と`ShaderGraphTechniques-Runtime-All.unitypackage`。
- 公開用映像: ローカル生成物`Artifacts/Captures/Videos/ShaderGraphTechniques-YouTube-Showcase-Final.mp4`の1ファイル。タイトル3秒＋6作例各10秒＋アウトロ2秒の65.000秒で、最後の1秒は動画サムネイルです。1920×1080／60fps、H.264、48kHzステレオAAC BGM付きです。カメラは全編固定し、効果本体と対象物だけを動かしています。6作例すべてに日本語の効果説明字幕があり、02は比較用の青い固定物体2個を非表示にしてバリアと動く柱だけを見せます。BGMの出典と利用条件は`Documentation/MUSIC.md`に記録しています。
- 技術確認素材: Gallery、6作例Perspective、02／03 Orthographicの旧9本も、比較・検証用の個別媒体として`Artifacts/Captures/Videos/`に保持しています。
- 移植: 7個の独立した新規URPプロジェクトで、個別6 PackageとRuntime-AllをImport・制御・実レンダー済みです。

固定時間刻み動画は実時間60fpsの性能証拠ではありません。XR、モバイル、別OS／GPU／Graphics APIは未検証です。YouTubeアップロード、公開動画URL、Zenn公開はまだ実施していません。

検証結果は`Documentation/VALIDATION.md`、撮影手順と生成媒体は`Documentation/CAPTURE.md`、BGMの出典と利用条件は`Documentation/MUSIC.md`、持ち出しとパッケージ内容は`Documentation/DISTRIBUTION.md`、公開時の未実施手順は`Documentation/PUBLISHING.md`を参照してください。
