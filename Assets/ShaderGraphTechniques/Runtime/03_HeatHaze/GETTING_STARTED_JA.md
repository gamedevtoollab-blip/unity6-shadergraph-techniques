# 03 Heat Haze 導入手順

1. 使用中のUniversalRenderPipelineAssetでOpaque Textureを有効にします。
2. `PF_03_HeatHaze.prefab`をカメラへ向け、不透明な背景の手前に置きます。
3. 静止比較や撮影では`HeatHazeController.SetManualTime(seconds)`、通常時間へ戻す場合は`UseRealtime()`、合成を消す場合は`SetOpacity(0)`を使います。

GraphはQuadのUV0でノイズを作り、Screen PositionでScene Colorを読みます。透明物を含む描画済み画面全体、Fog、前景輪郭の完全な再構成には対応しません。複数の透明表現の重なりは別途評価してください。

必要ファイルはこのフォルダー、`Runtime/Common/ManualTime.shadersubgraph`、`Runtime/Common/MaterialInstanceOwner.cs`、`Runtime/Common/LICENSE.txt`、`Runtime/ShaderGraphTechniques.Runtime.asmdef`です。
