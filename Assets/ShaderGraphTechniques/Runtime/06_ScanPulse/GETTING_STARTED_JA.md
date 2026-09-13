# 06 Scan Pulse 導入手順

1. 同じ波を見せるRenderer群へ`MAT_06_ScanPulse.mat`を割り当て、共通の親へ`MaterialInstanceOwner`と`ScanPulseController`を置きます。
2. `SetCenter(transform)`で中心を指定します。未指定時はコンポーネント自身の位置を使います。
3. `SetState(true, radius)`で波を表示し、半径を0へ戻す直前は`SetState(false, radius)`として中心の意図しない発光を防ぎます。

このGraphを使うMaterialだけに作用し、ポストエフェクトでも透視表示でもありません。球面距離はXYZを使うため、同じ中心・半径を渡した床、段差、壁、球体へ連続します。

必要ファイルはこのフォルダー、`Runtime/Common/MaterialInstanceOwner.cs`、`Runtime/Common/LICENSE.txt`、`Runtime/ShaderGraphTechniques.Runtime.asmdef`です。
