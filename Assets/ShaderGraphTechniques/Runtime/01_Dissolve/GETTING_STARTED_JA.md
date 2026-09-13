# 01 Dissolve 導入手順

1. `PF_01_Dissolve.prefab`をSceneへ置くか、任意のUV0付きMeshRendererへ`MAT_01_Dissolve.mat`を設定します。
2. 任意メッシュで動かす場合は、同じGameObjectへ`MaterialInstanceOwner`と`DissolveController`を追加します。
3. `DissolveController.SetProgress(0..1)`を呼びます。0は完全表示、1は完全消去です。

`_Progress`、`_NoiseScale`、`_EdgeWidth`、`_BaseColor`、`_EdgeColor`、`_EdgeIntensity`が公開Referenceです。GraphはOpaque＋Alpha Clippingです。見た目が消えてもColliderやゲーム上の生死判定は変わりません。影が本体と同じ切り抜きになることを使用先のライトとPlayerで確認してください。

必要ファイルはこのフォルダー、`Runtime/Common/MaterialInstanceOwner.cs`、`Runtime/Common/LICENSE.txt`、`Runtime/ShaderGraphTechniques.Runtime.asmdef`です。
