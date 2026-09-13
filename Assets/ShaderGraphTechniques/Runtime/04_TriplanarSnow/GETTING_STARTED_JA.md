# 04 Triplanar Snow 導入手順

1. `MAT_04_TriplanarSnow.mat`を任意のMeshRendererへ割り当てます。UV展開は岩色の投影に使いません。
2. `_BaseMap`はRepeat、sRGB、Mip Maps Onの繰り返し可能な色Textureにします。同梱`TEX_SeamlessRock.png`は自作生成済み例です。
3. `_SnowAmount`を0〜1で調整します。0は雪なし、1はWorld上向き判定で許可された面を埋めます。

Triplanarは3方向のTextureサンプルを行います。World投影なので物体を移動すると模様との位置関係が変わります。雪の厚み、屋根による遮蔽、体積計算はありません。垂直面や下向き面を一律に白くしません。

必要ファイルはこのフォルダーと`Runtime/Common/LICENSE.txt`です。
