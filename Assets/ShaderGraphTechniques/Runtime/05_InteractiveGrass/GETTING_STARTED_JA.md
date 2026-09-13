# 05 Interactive Grass 導入手順

1. `PF_05_InteractiveGrass.prefab`を置き、`InteractiveGrassController.Interactor`へ対象Transformを設定します。未設定時は接近変形が0になります。
2. 自作MeshではUV0.Yを根元0、先端1にし、縦方向を分割してください。同梱`MESH_GrassCluster.asset`は各草を6分割した生成済みMeshです。
3. `SetBend(radius, strength)`で許容変位を変えた場合はコンポーネントがRuntimeの`Renderer.localBounds`を再計算します。0・負・反転スケールは対象外です。

距離判定はXZの水平距離だけなので、多層構造では高さ条件か草グループ分離が必要です。変位はColliderを更新しません。World位置で計算後、Position型のWorld→Object Transformを通します。手動時間は`SetManualTime`、通常時間は`UseRealtime`です。

必要ファイルはこのフォルダー、`Runtime/Common/ManualTime.shadersubgraph`、`Runtime/Common/MaterialInstanceOwner.cs`、`Runtime/Common/LICENSE.txt`、`Runtime/ShaderGraphTechniques.Runtime.asmdef`です。
