# 検証記録

この文書は、2026-09-12〜13に実行して結果ファイルを取得できた検証だけを記録します。

## 固定環境

| 項目 | 実測値 |
|---|---|
| Unity Editor | `6000.3.22f1` (`1c726e1fb402`) |
| URP | `17.3.0` |
| Shader Graph | `17.3.0` |
| Color Space | Linear |
| Renderer | Universal Renderer / Forward / Render Graph標準経路 |
| Player | Windows Standalone 64-bit、単一Base Camera |
| 実キャプチャGPU | NVIDIA GeForce RTX 3060 Laptop GPU、Direct3D 12、driver `32.0.15.9227` |

## 完了した検証

### プロジェクト構造とGraph

`ShaderGraphTechniqueProjectValidator.RunAll`は`68 passed / 0 failed`です。次を全件検査しました。

- Editor／URP／Shader Graphの版、Linear、Depth Texture、Opaque Texture、Forward Renderer。
- 6個の編集可能な`.shadergraph`と`ManualTime.shadersubgraph`、必須Reference名、標準ノード型、グループ、Shaderコンパイル結果。
- Intersection ShieldのScene Depth Difference Eyeモード、雪のWorld投影、草のWorld→Object Position変換。
- Material、6個のRuntime Prefab、Gallery＋6個別Scene、草Meshの7段のUV0.Y（縦6分割）。
- `Runtime → Demo/Editor/Tests`依存がないこと、Runtime PrefabにCamera／Light／Volume／Canvas／Demo Managerがないこと。

証跡は`Artifacts/Validation/project-validation.json`と`project-validation.md`です。

### EditModeとPlayMode

- EditMode: `9/9 passed`、`0 failed`、`0 skipped`。
  - Runtime Prefab 6個の不足スクリプトとPresentation混入なし。
  - `MaterialInstanceOwner`が1回だけ複製し、再利用し、共有Materialを変更せず、元参照を復元すること。
  - 草Mesh、Runtime依存方向、7個のBuild Scene。
  - 全Runtime Prefabを移動した親の下へ置き、親回転を与え、`1×`、`0.5×`、`2×`、正の非一様スケール`(0.5, 2, 1.25)`で、正の行列式、有限Bounds、対応Shader、草InteractorとScan中心のWorld値を確認。
- PlayMode: `4/4 passed`、`0 failed`、`0 skipped`。
  - 6 Shaderが解決・対応し、RenderTextureへ1フレーム描画できること。
  - Dissolveを2個配置して`Progress=0/1`を別々に設定しても共有Materialと相互に干渉しないこと。
  - 草の対象未設定が非作動になり、非一様スケールでもBoundsが有限であること。
  - Scanを`Active=0, Radius=0`へ戻すと発光状態が残らないこと。

02の固定比較物を非表示にした動画演出変更後にも再実行し、`Artifacts/Validation/editmode-results-clean-shield.xml`と`playmode-results-clean-shield.xml`で同じ`9/9`、`4/4`を確認しました。

### Playerビルドと起動

StrictModeのWindows Playerビルドは`Succeeded`、`0 errors / 0 warnings`です。Galleryを先頭に6個別Sceneを含む7 Scene、02整理後のBuildPipeline報告サイズは`102,457,146 bytes`でした。

Player一式は189ファイル、実ファイル合計`102,650,651 bytes`です。各相対パス・サイズ・SHA-256から計算したツリーSHA-256は次です。

```text
3d62062ecc7f8d714d63ef5ad23006ab84bbb08b25102a356a3a2fc7029a9356
```

証跡は`Artifacts/Validation/player-build-summary.json`と`player-build-file-manifest.json`です。Playerは実際に起動し、Gallery、6個別Scene、バリア／熱波のOrthographicを読み込み、PNG連番を生成して正常終了しました。

### 配布Packageの内容

6作例別＋Runtime-Allの7個を展開し、全pathname、GUID、asset SHA-256を`package-manifest.json`と照合して全件PASSしました。

- Demo、Tests、Editor、ProjectSettings、別作例の混入なし。
- 必要なGraph／Sub Graph／Material／Texture／Mesh／Script／asmdef／導入ガイド／Asset内LICENSEの閉包を確認。
- Asset内LICENSEのGUIDは全Packageで`35b705c95a276624ebe82a60b7ebd085`に一致。

証跡は`Artifacts/Validation/package-content-validation.json`です。

### 作例単位の隔離移植

元プロジェクトのAssetsやLibraryをコピーせず、Unity CLIから新規作成した7個の独立URPプロジェクトへ、6作例を各1個ずつ、およびRuntime-AllをImportしました。全プロジェクトでUnity `6000.3.22f1`、URP／Shader Graph `17.3.0`を確認しています。

全7 ImportがPASSしました。各作例についてPrefab／Graph／対応Shader、不足スクリプト0、制御APIまたは公開Material値、状態A/Bの実Unityレンダー、画像差分、マゼンタFallbackなしを検査しました。Runtime-Allでは同一Import後に6作例すべてを個別レンダーしています。

証跡は`Artifacts/Validation/migration-validation-summary.json`、7個の`Artifacts/Validation/Migration/*/migration-validation.json`、状態画像、`contact-state-a.png`／`contact-state-b.png`です。試験用プロジェクト本体は`Artifacts/MigrationProjects/`に保持していますが、公開ソース履歴からは除外します。

### 視覚確認と媒体

Gallery、6作例のPerspective、Intersection Shield／Heat HazeのOrthographicを実Playerから固定時間刻みで撮影し、開始・中間・終了フレームと移植試験の状態A/Bを目視確認しました。旧9動画の機械検証は全件PASSです。

公開用候補として、タイトル3秒＋6作例各10秒＋アウトロ2秒を1本へまとめた`ShaderGraphTechniques-YouTube-Showcase.mp4`を生成しました。65.000秒、3,900フレーム、1920×1080、60fps、H.264 High、`yuv420p`で、全編デコードに成功しています。Mixkitの`Digital Clouds`をAAC LC、48kHzステレオで埋め込み、音声長65.000秒、実測`-15.7 LUFS`、True Peak `-1.1 dBFS`を確認しました。BGM追加前後の全3,900デコードフレームはハッシュ一致し、映像内容に変更がないことを確認しました。カメラは全編固定し、Galleryを含む7連番の全3,780フレームで各Sceneのカメラ姿勢・画角が1種類だけであることを`camera-trace.csv`から確認しました。各作例に日本語の効果説明字幕を表示し、02は比較用の青い固定物体2個をShowcase中だけ非表示にして、拡大したバリアを唯一の柱が横断して交差状態で停止します。各作例の1／3／5／7／9秒を抽出した30サンプルは30個すべて別ハッシュで、モーション接触シート、字幕6本、02の交差中フレームも目視確認しました。

最終公開候補`ShaderGraphTechniques-YouTube-Showcase-Final.mp4`では、上記映像の`64.000`〜`65.000`秒だけをYouTube用サムネイルへ置き換え、BGMと総尺を維持しました。映像・音声とも65.000秒、3,900フレーム、1920×1080、60fps、H.264 High／`yuv420p`、AAC LC 48kHzステレオです。ファイルサイズは`8,676,282 bytes`、SHA-256は`7a837ad5f17dc15b5f0210a70481a4910a16ab90c7016b7b45c9b09d288d8e73`です。境界直前、切替後、終了直前の抽出フレームを目視確認しました。詳細は`Documentation/CAPTURE.md`、`Documentation/MUSIC.md`、`Artifacts/Validation/media-validation.json`、`Artifacts/Validation/Showcase/showcase-video-validation.json`にあります。

### GitHub fresh clone検証（2026-09-13）

`gamedevtoollab-blip/unity6-shadergraph-techniques`のfresh cloneへ公開対象ファイルだけを配置し、Unity `6000.3.22f1`でLibraryなしの状態から再importしました。URP／Shader Graph `17.3.0`を含む57 Packageの解決とスクリプトコンパイルに成功し、EditMode `9/9`、実GPU描画を使うPlayMode `4/4`がPASSしました。`-nographics`を付けたPlayMode試行は、4件ともNull Graphics上の`RenderTexture.Create failed`となったため採用していません。採用結果XMLはGit管理外の`Artifacts/Validation/GitHubClone/`に保存しています。

## 修正してから再検証した事項

- 初回Playerビルドでは、頂点変形を行わない5 GraphのMotion Vectorパスに`BuildVertexDescriptionInputs`不足が出ました。標準`Position(Object) → Vertex Position`パススルーを明示し、最終StrictModeビルドを成功させました。
- 初期のPlayMode試験はBuild ProfileのScene解決とbatchmode待機方法に問題があり、採用していません。最終XML付き実行だけを上記結果に採用しています。
- Playerウィンドウを非表示にした試行では黒いScreenCaptureが生成されたため採用せず、可視レンダリングで再撮影しました。
- Heat Hazeの見出しと背景格子の重なりを生成元で直し、Scene再生成、Player再ビルド、再撮影を行いました。
- Showcase初回試行では、非同期`CaptureScreenshot`が同一バックバッファを複製して動きが記録されず、シーン切替時の重複排除が移動先の演出コンポーネントも削除していました。現在は現在Sceneの演出だけを選び、各フレームへ手動時刻を注入し、同期テクスチャ取得する方式へ修正しています。Galleryの操作UI混入も修正後に再撮影しました。
- 旧Showcaseのカメラ移動を廃止し、Scene読込時の位置・回転・画角をLateUpdateでも復元する固定構図へ変更しました。02はバリアの表示を拡大し、柱の外側待機、横断、交差中停止を追加しました。6作例すべての下部へ日本語の効果説明字幕を焼き込み、全素材を再撮影・再合成しています。
- 02の動く柱と重なって注意を奪っていた`Side Pillar`と、左側の`Occlusion Check`は、技術Sceneには残したままShowcaseモードで非表示にしました。バリアと動く柱だけの構図で全素材を再撮影・再合成しています。
- BGM追加前の映像へMixkitのフリーBGMを追加しました。音声付きMP4に対応するよう生成・検査スクリプトを更新し、全編デコード、AAC仕様、音声長、ラウドネス、True Peak、30点の映像モーションハッシュを再検証しました。

## 未検証・対応範囲外

- 固定時間刻み60fpsは実時間60fps性能の証拠ではありません。Profilerによる性能測定値はありません。
- XR、モバイル、macOS／Linux、別GPU、Direct3D 11／Vulkan／Metal、Built-in／HDRP／2D Rendererは未検証です。
- ゼロスケールと負スケールは対象外です。
- 実機ゲームへの長時間組み込み、複数Camera、Camera Stack、Fog、透明物を含むHeat Hazeの合成、複雑な地形での品質は未検証です。
- GitHubソース公開先は`https://github.com/gamedevtoollab-blip/unity6-shadergraph-techniques`です。生成済みPackage／Player／動画のRelease添付、YouTube公開、記事公開、実際の動画URL・開始時刻は未作成です。YouTubeへアップロードした後のContent ID判定と、再生環境での人による全編試聴は未実施です。
- Shader Graph Editor画面の公開用スクリーンショットは未生成です。掲載済みと扱いません。
