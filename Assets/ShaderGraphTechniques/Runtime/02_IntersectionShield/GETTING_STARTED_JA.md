# 02 Intersection Shield 導入手順

1. 使用中のUniversalRenderPipelineAssetでDepth Textureを有効にします。
2. `PF_02_IntersectionShield.prefab`を不透明な床や柱と交差させ、カメラを球の外側へ置きます。
3. 任意メッシュへ使う場合は`MAT_02_IntersectionShield.mat`を割り当てます。基本形はTransparent／Alpha、Front面、ZWrite Off、ZTest LEqualです。

Graphは`Scene Depth Difference / Eye`を使います。交差帯は画面深度差でありCollider接触や物体間最短距離ではありません。透明物同士の交差を一般に検出せず、手前の不透明物を透視しません。Orthographicでは帯の見え方がPerspectiveと異なるため、対象カメラで確認してください。

必要ファイルはこのフォルダーと`Runtime/Common/LICENSE.txt`です。
