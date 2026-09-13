---
title: "Unity 6.3 Shader Graphで作る、ゲームに持ち込みたくなる表現6選"
emoji: "✨"
type: "tech"
topics: ["unity", "shadergraph", "urp", "shader", "gamedev"]
published: false
---

## はじめに

敵が燃えるように消える。バリアと地面の接点が光る。熱源の向こうの景色が揺れる。

こうした表現は、複雑な画像を用意することよりも、**「何を基準に、どの部分を変えるか」**を決めるところから作れます。

この記事では、Unity 6.3のURPとShader Graphを使い、見た目も仕組みも異なる6つの表現を組み立てます。

| 作例 | 完成イメージ | 中心になる考え方 |
| --- | --- | --- |
| 01 発光ディゾルブ | 発光する縁を残して、物体がまだらに消える | ノイズをしきい値で切り抜く |
| 02 接触バリア | 地面や障害物と交差した場所が光る | 画面上の深度差を使う |
| 03 ヒートヘイズ | 熱源の向こうの背景が揺らぐ | 背景を読む位置をずらす |
| 04 トライプラナー積雪 | UV展開なしで岩を描き、上向きの面に雪を載せる | 投影座標と面の向きを使う |
| 05 インタラクティブな草 | 根元を残し、風やキャラクターの接近で草がしなる | 頂点を高さに応じて動かす |
| 06 スキャン波 | 光の帯が床から壁、建物へと連続して走る | ワールド空間の距離を使う |

狙いは、動画で見せるだけの一発芸ではありません。自分のゲームに持ち込んだときに、変更する値と必要な設定が分かる作例にします。

:::message
本稿はUnity 6.3とShader Graph 17.3の公式仕様を確認し、対応サンプルを実装・検証した手順です。Unity `6000.3.22f1`、URP／Shader Graph `17.3.0`、Linear、Universal Renderer Forwardで、6作例の実行、Windows Playerビルド、作例別PackageとRuntime-Allの隔離移植、実Player映像の生成まで確認しました。固定時間刻み60fpsは動画ストリームの条件であり、実時間性能の実測値ではありません。
:::

実装サンプルはリポジトリ内の`unity/unity63-shadergraph-six-techniques/`、GraphとPrefabは`Assets/ShaderGraphTechniques/Runtime/`、Galleryは`Assets/ShaderGraphTechniques/Demo/Scenes/00_Gallery.unity`にあります。検証記録は`Documentation/VALIDATION.md`、配布物は`Artifacts/Packages/`、実映像は`Artifacts/Captures/Videos/`です。

<!-- PUBLICATION: Git remoteと確認コミットは未設定。実在する公開URLが確定してから追記する。 -->
<!-- PUBLICATION: YouTubeは未アップロード。実在する動画IDと実タイムコードが確定してから冒頭へ1件埋め込む。 -->

## 共通の準備

### 対象環境

Unity 6.3は`6000.3`系です。6.3の公式マニュアルは、この世代のShader Graph機能について17.3のパッケージドキュメントを参照しています。本稿もこの組み合わせを対象にします。6つすべてが「6.3で初めて可能になった表現」という意味ではありません。[^unity63]

| 項目 | 本稿の前提 |
| --- | --- |
| Editor | Unity 6.3 LTS／`6000.3.22f1` (`1c726e1fb402`) |
| レンダーパイプライン | URP `17.3.0`／Shader Graph `17.3.0` |
| Renderer | 3D用のUniversal Renderer、Forwardを基準とする |
| Color Space | Linear |
| カメラ | 単一のBase Camera。最初はPerspective |
| グラフの精度 | Singleを基準とする |
| 基本の対象 | 通常のMeshRenderer。HDRP・Built-in・2D Rendererは本稿の対象外 |

Universal 3D系のテンプレートから始め、Package Managerで実際に解決された版を確認します。別世代のURPやShader Graphへ無理に差し替えるのではなく、`ProjectVersion.txt`、`manifest.json`、`packages-lock.json`を残して環境を再現できるようにします。

作例のシーンでは、1 Unity unit = 1 mとして距離と変位量を調整します。異なるスケールのゲームへ移す場合は、距離系のプロパティも合わせて調整してください。

今回の作例に、独自のRenderer Featureや手書きのシェーダ本体は必要ありません。見た目はShader Graphで作り、C#はパラメーターのアニメーションやキャラクター位置の受け渡しに限定します。

### Depth TextureとOpaque Textureは別のもの

URP Assetの`Depth Texture`は深度を読むため、`Opaque Texture`は不透明物の描画後の背景色を読むための設定です。カメラ側で上書きされる場合もあります。[^urp-asset]

| 作例 | Depth Texture | Opaque Texture |
| --- | --- | --- |
| 接触バリア | 必須 | 不要 |
| ヒートヘイズ | 基本形では不要 | 必須 |
| それ以外 | 作例の計算には不要 | 不要 |

サンプル全体で有効にしておくことと、自分のゲームへ持ち込む作例に本当に必要な設定を説明することは分けて考えます。Graphics設定だけでなく、使用中のQualityレベルとカメラがどのURP Asset・Rendererを使うかも確認してください。

ヒートヘイズの比較では、まず`Opaque Downsampling = None`にします。縮小した背景テクスチャを使う場合は、歪みとは別に解像感が変わります。[^urp-asset]

### 「明るい色」とBloomを分けて調整する

発光部分は、Lit Graphなら`Emission`、Unlit Graphなら`Base Color`へ明るい値を渡して作ります。**周囲へにじむ見た目はBloom側の仕事**です。[^lit][^unlit][^bloom]

Bloomを使う場合は、URPとカメラのHDR設定、カメラの`Post Processing`、Global VolumeとVolume Profile内のBloom、さらにカメラのVolume MaskとVolumeのLayerを確認します。Volumeを置くだけで終わりにしないことが重要です。[^camera][^post]

作例はBloomなしでも仕組みが判別できるように作り、最後に弱めのBloomを足します。比較用の最小シーンではFogを使わず、TAA・モーションブラー・動的解像度も必須条件にしません。

### 数式をノードへ読み替える

以下の`text`ブロックは、貼り付けて実行するHLSLではなく、**ノードの接続を短く表した式**です。

`Saturate`は0〜1に収めるノードです。`Step(edge, x)`は、`x < edge`なら0、それ以外なら1になります。`Smoothstep(a, b, x)`は、`a`から`b`にかけて滑らかに0から1へ変化させます。本稿では常に`a < b`になるようにします。[^step][^smoothstep]

`Pws`は`Position / World`、`PosOS`は`Position / Object`です。`.xz`などは、`Split`と`Combine`、または`Swizzle`で必要な成分を取り出すという意味です。今回はURP内でワールド座標を統一して使います。[^position][^transform]

公開するプロパティは、分かりやすい表示名に加えて、本文の`_Progress`などに合わせた**Reference名**を設定します。Shader Graph 17.3では、マテリアルInspectorへの表示は`Show In Inspector`という項目で指定します。[^properties]

#### 動く表現には、止められる時間も用意する

作例03と05で使う`t`は、普段は`Time`ノードの`Time`出力です。撮影や比較用には、次の小さなSub Graphにしておくと、任意の時刻で静止できます。[^time]

```text
t = Lerp(Time.Time, _ManualTime, Saturate(_UseManualTime))
```

`_UseManualTime`は0〜1のFloatで、初期値0。`_ManualTime`は秒を表すFloatで、初期値0です。入力を明示したSub Graphとして共通化し、撮影専用のグローバル変数には依存させません。

## 01 発光する境界を残して消えるディゾルブ

実装は`Assets/ShaderGraphTechniques/Runtime/01_Dissolve/`、ローカル確認動画は`Artifacts/Captures/Videos/01_Dissolve-Perspective.mp4`です。公開URLとGraph Editor画像は未設定です。

敵の消滅、召喚、アイテムの出現に使いやすい表現です。透明度を全体的に下げるのではなく、ノイズに沿って一部分ずつ消します。

### グラフとプロパティ

URPのLit Graphを作り、`Surface Type = Opaque`、`Alpha Clipping = On`にします。Alpha Clippingは、Alphaがしきい値未満のピクセルを破棄する設定です。[^lit]

| Reference | 型・初期値 | 役割 |
| --- | --- | --- |
| `_Progress` | Float、0〜1、初期値0 | 0で完全表示、1で完全消去 |
| `_NoiseScale` | Float、8 | 模様の細かさ |
| `_EdgeWidth` | Float、0.06 | 発光帯の幅 |
| `_BaseColor` | Color、好みの本体色 | 通常の表面色 |
| `_EdgeColor` | HDR Color、橙系 | 発光色 |
| `_EdgeIntensity` | Float、3 | 発光の強さ |

最初の対象は、UV0を持つSphereやCapsuleにします。UVを使う方式なので、UVのないメッシュや大きく重なったUVでは、意図した模様になりません。

### 接続の中心

`Simple Noise`へUV0のXYと`_NoiseScale`を入力します。出力を`Saturate`に通したものを`m`とします。[^noise]

```text
p       = Saturate(_Progress)
w       = Max(_EdgeWidth, 0.0001)
m       = Saturate(SimpleNoise(UV0.xy, _NoiseScale))

cut     = Lerp(-w - 0.0001, 1.0001, p)
visible = Step(cut, m)
edge    = visible * (1 - Smoothstep(cut, cut + w, m))
```

`Step`の`Edge`に`cut`、`In`に`m`を接続します。発光帯は、残っている側の境界から幅`w`の範囲だけに作ります。

```text
Simple Noise ── Saturate ──┬─ Step ─────────────── Alpha
                          │
                          └─ Smoothstep → One Minus
                                      × visible
                                      × HDR Color
                                      × Intensity ── Emission
```

Fragmentへの出力は次のとおりです。

```text
Base Color           = _BaseColor.rgb
Emission             = _EdgeColor.rgb * _EdgeIntensity * edge
Alpha                = visible
Alpha Clip Threshold = 0.5
Metallic             = 0
Smoothness           = 0.35
```

### なぜしきい値を少し外へ広げるのか

単純に`Step(_Progress, m)`とすると、端の値の扱いが曖昧になります。特に`m = 1`の場所は、`_Progress = 1`でも残り得ます。

ここでは、0のときのしきい値を`-w - ε`、1のときを`1 + ε`にしています。`m`が0〜1なら、**開始時には発光帯も切り抜きもなく、終了時には完全に消える**という端点を作れます。これは上の式から決まる性質です。ただし、Progressの数値と「消えた面積の割合」は一致しません。

### 持ち込むときの注意

このGraphの切り抜きは見た目の処理です。Colliderやゲーム上の生死判定は別に制御します。影についても、本体が消えているのに影だけ残っていないかを、実際のライトとPlayerビルドで確認します。

発光が白くつぶれる場合は、まずBloomではなく`_EdgeIntensity`を下げて境界の形を確認します。透明ブレンドへ変更して問題を隠すのではなく、Opaque＋Alpha Clippingのまま完成させる作例です。

## 02 地面や障害物との接点が光るバリア

実装は`Assets/ShaderGraphTechniques/Runtime/02_IntersectionShield/`、ローカル確認動画はPerspective／Orthographicの2本です。公開URLとGraph Editor画像は未設定です。

透明な球体を置き、その内側へ床や柱を食い込ませます。交差する場所に光の帯が現れると、単なる透明な球よりも「エネルギーの膜」らしく見せられます。

### グラフとプロパティ

URPのUnlit Graphで、次の設定にします。透明ブレンド、描画面、深度書き込みはGraph Inspectorで指定できます。[^unlit]

```text
Surface Type  = Transparent
Blending Mode = Alpha
Render Face   = Front
Depth Write   = Force Disabled
Depth Test    = L Equal
Cast Shadows  = Off
```

最初はカメラを球の外へ置きます。両面描画は、表裏の透明面の重なりまで増やすため、基本形では使いません。

| Reference | 型・初期値 | 役割 |
| --- | --- | --- |
| `_ContactWidth` | Float、0.15 | 光らせる深度差の幅 |
| `_RimPower` | Float、4 | 輪郭の締まり方 |
| `_RimIntensity` | Float、2 | 輪郭の明るさ |
| `_ContactIntensity` | Float、5 | 接触帯の明るさ |
| `_ShieldColor` | HDR Color、青緑系 | バリアの色 |
| `_Opacity` | Float、0〜1、初期値1 | 表示の強さ |

### 深度差は専用ノードで取る

`Scene Depth Difference`を追加し、Sampling Modeを`Eye`にします。`Scene UV`には`Screen Position / Default`、`Position WS`には`Position / World`を接続します。

このノードは、画面上の深度と指定したワールド位置の深度の差を返します。Eyeモードの単位はメートルで、サンプルした面のほうがカメラに近い場合は負の値になります。[^depth-difference]

```text
d       = SceneDepthDifference(Eye)
w       = Max(_ContactWidth, 0.0001)
contact = Step(0, d) * (1 - Saturate(d / w))
```

深度が近いほど`contact`が1に近づき、離れると0になります。これはカメラの奥行き方向に沿う深度差であり、**物体間の最短距離やCollider同士の接触判定ではありません**。画角を変えると帯の見え方も変わります。

さらに`Fresnel Effect`を追加します。NormalとView Directionを同じWorld空間にそろえ、Powerに`_RimPower`を渡します。出力を`rim`とします。Fresnel Effectは、視線に対して斜めを向いた面を強調するためのノードです。[^fresnel]

```text
Base Color = _ShieldColor.rgb
             * (0.2 + rim * _RimIntensity
                    + contact * _ContactIntensity)

Alpha = Saturate((0.04 + 0.25 * rim + 0.8 * contact) * _Opacity)
```

### 光らないときに確認すること

まず、使用中のURP AssetとカメラでDepth Textureが有効かを確認します。次に、交差させる床や柱を、URP/Litなどの通常の不透明マテリアルにします。

この方式が読めるのは、その描画時点の深度テクスチャに入っている面です。透明な物体すべてとの交差が取れるわけではありません。バリア自体のDepth Writeを有効にして「直したつもり」にしないことも大切です。

カメラの手前に不透明な箱を置き、箱の向こうのバリアが通常どおり隠れることも確認します。深度テストを`Always`へ変えると、それは別の透視表現になってしまいます。

## 03 背景をゆがめるヒートヘイズ

実装は`Assets/ShaderGraphTechniques/Runtime/03_HeatHaze/`、ローカル確認動画はPerspective／Orthographicの2本です。公開URLとGraph Editor画像は未設定です。

炎、排気口、魔法陣、熱い地面の上などに使える陽炎です。

ここでは「半透明の色を重ねる」のではなく、**背景の画像を少しずれた位置から読み直す**ことで、空気が揺らいでいるように見せます。

### グラフとプロパティ

URPのUnlit Graphを使い、`Transparent / Alpha`、`Depth Write = Force Disabled`、`Depth Test = L Equal`、`Cast Shadows = Off`にします。最初はカメラへ表面を向けたQuadに適用します。

| Reference | 型・初期値 | 役割 |
| --- | --- | --- |
| `_NoiseScale` | Float、6 | 歪み模様の細かさ |
| `_Distortion` | Float、0.01 | 正規化画面UVでの歪み量 |
| `_Opacity` | Float、0〜1、初期値1 | 歪んだ背景を混ぜる割合 |
| `_FlowA` | Vector2、(0.12, 0.20) | 1つ目のノイズの移動 |
| `_FlowB` | Vector2、(-0.18, 0.10) | 2つ目のノイズの移動 |
| `_ScreenEdgeFade` | Float、0.04 | 画面端で歪みを弱める幅 |

加えて、共通の`_UseManualTime`と`_ManualTime`を用意します。

### QuadのUVと画面UVを使い分ける

歪み模様はQuadのUV0から作り、背景を読む位置には`Screen Position / Default`のXYを使います。Defaultは、画面を0〜1で扱うための座標です。Rawとは異なります。[^screen-position]

```text
u = UV0.xy
s = ScreenPosition(Default).xy

n1 = SimpleNoise(u * _NoiseScale + t * _FlowA, Scale = 1)
n2 = SimpleNoise(u * _NoiseScale + t * _FlowB + (19.7, 3.1), Scale = 1)
v  = 2 * Vector2(n1, n2) - Vector2(1, 1)
```

2つのノイズをまとめた`v`を、背景をずらす2方向の成分として使います。

次にQuadの外周をぼかすマスクを作ります。

```text
r        = Length(u * 2 - 1)
shape    = 1 - Smoothstep(0.65, 1, r)
edgeDist = Min(Min(s.x, 1 - s.x), Min(s.y, 1 - s.y))
screen   = Smoothstep(0, Max(_ScreenEdgeFade, 0.0001), edgeDist)

sampleUV = Clamp(s + v * _Distortion * shape * screen,
                 (0.001, 0.001), (0.999, 0.999))
```

`shape`でQuadの四角い縁を消し、`screen`で画面端へ近づくほど歪みを弱めます。Clampの値はこの基本形の固定マージンであり、厳密な半テクセル補正ではありません。

`sampleUV`を`Scene Color`のUV入力のXYへ渡し、出力をそのまま色として使います。Vector4へ組み立てる場合、未使用のZWは0にします。

```text
Base Color = SceneColor(sampleUV)
Alpha      = shape * Saturate(_Opacity)
```

RGBへAlphaを先に掛けません。ここではAlphaブレンド側で合成させます。また、Scene ColorへHDRの倍率を掛けて発光させる作例でもありません。

### この方法で読める背景には限界がある

URPのScene Colorが読むのは、透明物の描画前にコピーされたOpaque Textureです。Fragment段階で使用します。ガラスやパーティクルなどを含んだ「描画済み画面の全部」を自由にゆがめる機能ではありません。[^scene-color]

そのため、透明物との重なりや複数のヒートヘイズの重なりでは、先に描いた透明表現を上書きしたように見えることがあります。描画順を変えるだけで、背景コピーに含まれていない情報を取り戻すことはできません。

また、画面上の読む位置をずらしているだけなので、前景の輪郭付近で別の物体の色を引き込む場合があります。画面端のフェードやClampは、すべての輪郭問題を解決するものではありません。

基本形は、Fogなし、不透明な背景、単独のQuadで評価します。停止時の比較では、`_Opacity = 0`またはRendererの無効化を使います。**歪み量を0にするだけでは、背景コピーを描く処理そのものは消えません。**

## 04 UV展開なしで岩を描き、上向きの面に雪を載せる

実装は`Assets/ShaderGraphTechniques/Runtime/04_TriplanarSnow/`、ローカル確認動画は`Artifacts/Captures/Videos/04_TriplanarSnow-Perspective.mp4`です。公開URLとGraph Editor画像は未設定です。

UVが整っていない岩や崖にも模様を貼り、その上へ雪が広がる表現です。雪色を緑や暗い茶色に変えると、苔や汚れの被覆にも応用できます。

### グラフとプロパティ

URPのLit Graphで、`Surface Type = Opaque`、`Alpha Clipping = Off`にします。

| Reference | 型・初期値 | 役割 |
| --- | --- | --- |
| `_BaseMap` | Texture2D | 繰り返し可能な岩の色テクスチャ |
| `_BaseTint` | Color、白 | 岩色の調整 |
| `_TextureScale` | Float、1 | ワールド空間のテクスチャ密度 |
| `_SnowAmount` | Float、0〜1、初期値0.5 | 雪が広がる量 |
| `_SnowColor` | Color、少し青みのある白 | 雪の色 |
| `_SnowNoiseScale` | Float、1.5 | 被覆模様の細かさ |
| `_SnowSoftness` | Float、0.12 | 雪の境界の柔らかさ |

`_BaseMap`は色テクスチャとしてsRGBを有効にし、Wrap ModeをRepeat、Mip Mapsを有効にしたものを使います。[^texture-import]配布サンプルには、自作したシームレスな`TEX_SeamlessRock.png`と生成元を含めました。

### 岩はTriplanarで貼る

`Triplanar`ノードのTypeを`Default`、Input Spaceを`World`にします。`_BaseMap`をTexture、`_TextureScale`をTileへ渡し、Blendは4から始めます。PositionとNormalもWorld空間でそろえます。

Triplanarは3方向からテクスチャを投影し、面の向きに応じて混ぜます。入力テクスチャを3回サンプルするため、通常の1回のテクスチャ参照と同じ負荷ではありません。このノードはFragment段階で使います。[^triplanar]

```text
rock = Triplanar(_BaseMap,
                 Position = Pws,
                 Normal   = Normalize(NormalVector(World)),
                 Tile     = _TextureScale,
                 Blend    = 4).rgb * _BaseTint.rgb
```

### 雪は「上向き」と「広がり方」を掛ける

面の向きには`Normal Vector / World`を使います。`(0, 1, 0)`とのDot Productで上向きの度合いを作ります。[^normal]

```text
up    = Dot(Normalize(NormalVector(World)), (0, 1, 0))
slope = Smoothstep(0.35, 0.85, up)

n     = Saturate(SimpleNoise(Pws.xz, _SnowNoiseScale))
a     = Saturate(_SnowAmount)
s     = Max(_SnowSoftness, 0.0001)
cut   = Lerp(1 + s + 0.0001, -s - 0.0001, a)
cover = Smoothstep(cut - s, cut + s, n)
snow  = slope * cover
```

雪を置ける面を`slope`で制限し、その中をノイズの`cover`で埋めていきます。この式ではSnow Amountが0なら雪なし、1なら`slope`で許可した面が埋まります。1でも垂直な壁まで真っ白にはしません。

```text
Base Color = Lerp(rock, _SnowColor.rgb, snow)
Smoothness = Lerp(0.2, 0.35, snow)
Metallic   = 0
```

### これは積雪の「見た目」であり、体積計算ではない

この基本形は、色と表面の見え方を変えるだけです。雪の厚みで輪郭が膨らむことも、屋根に遮られた場所を判別することもありません。上向きの面なら、屋内でも雪が載り得ます。

また、ワールド投影なので、オブジェクトを動かすと模様との位置関係が変わります。静止した岩や建物向けです。動く小物に貼り付いた模様が必要なら、投影座標をObject側へ設計し直します。その際も、雪を上向きに限定する法線判定はWorldに残せます。

「UV不要」は「入力や条件が何も要らない」という意味ではありません。テクスチャのRepeat設定、法線、投影の座標空間が、この作例の入力契約です。

## 05 キャラクターが近づくと押し分けられる草

実装は`Assets/ShaderGraphTechniques/Runtime/05_InteractiveGrass/`、ローカル確認動画は`Artifacts/Captures/Videos/05_InteractiveGrass-Perspective.mp4`です。公開URLとGraph Editor画像は未設定です。

風に揺れる草へ、キャラクターの接近によるしなりを加えます。

この作例の中心は、**根元の頂点を動かさず、先端ほど大きく動かすこと**です。色ではなくVertex側を操作します。

### 先にメッシュの条件を決める

基本形では、草の輪郭を細いポリゴンで作ります。Alpha Clipping用の草画像は使いません。

1本の草を縦方向に6分割程度した、先端へ細くなる帯状メッシュにします。UV0のYを根元で0、先端で1にしてください。複数本を1つのメッシュにまとめても、各草のUV0.Yはそれぞれ0〜1です。

分割のない1枚のQuadは、途中でしなる曲線を表現できません。配布する場合は、メッシュ生成スクリプトだけでなく、生成済みのMeshアセットも含めます。

### グラフとプロパティ

URPのUnlit Graphで、`Opaque`、`Alpha Clipping = Off`、`Render Face = Both`にします。基本形はライティングを計算しないため、変形後の法線を再構築する問題を持ち込まずに済みます。[^unlit]

| Reference | 型・初期値 | 役割 |
| --- | --- | --- |
| `_InteractorPositionWS` | Vector3、(0, 0, 0) | 押し分ける対象のワールド位置 |
| `_InteractorEnabled` | Float、0〜1、初期値0 | 0で接近による変形を停止 |
| `_BendRadius` | Float、1 | 影響する水平距離 |
| `_BendStrength` | Float、0.6 | 押し分ける変位量 |
| `_WindDirectionXZ` | Vector2、(1, 0) | 風の方向 |
| `_WindStrength` | Float、0.08 | 風の変位量 |
| `_WindSpeed` | Float、1.5 | 風の変化速度 |
| `_RootColor` | Color、暗めの緑 | 根元の色 |
| `_TipColor` | Color、明るめの緑 | 先端の色 |

加えて共通の手動時間プロパティを用意します。距離と変位量は、今回の1 Unity unit = 1 mというシーン設計で調整します。

### 頂点の変位を作る

ここからの`Pws`は、Vertex側で評価する、変形前の`Position / World`です。

```text
h       = Saturate(UV0.y)
rootMask = h * h

q       = Pws.xz - _InteractorPositionWS.xz
d       = Length(q)
outward = q / Max(d, 0.0001)
radius  = Max(_BendRadius, 0.0001)
weight  = (1 - Smoothstep(0, radius, d)) * Saturate(_InteractorEnabled)

windDir = _WindDirectionXZ / Max(Length(_WindDirectionXZ), 0.0001)
phase   = Pws.x * 0.7 + Pws.z * 0.9 + t * _WindSpeed
wind    = windDir * Sin(phase) * _WindStrength

moveXZ  = (outward * weight * _BendStrength + wind) * rootMask
movedWS = Pws + Vector3(moveXZ.x, 0, moveXZ.y)
```

`h * h`が0になる根元は動かず、先端へ向かって変位が大きくなります。中心と完全に重なって`d = 0`になっても、ゼロ除算しない形にしています。その点では押し分け方向は0となり、風の成分だけが残ります。

最後に`movedWS`を`Transform / World → Object / Type = Position`へ通し、Vertexの`Position`へ接続します。

```text
Position(World) → 変位を加える
                → Transform(World → Object, Position)
                → Vertex / Position
```

VertexのPosition入力はObject空間です。World空間の位置をそのまま接続しないようにします。Transformの`Position`は移動を含む位置の変換で、`Direction`とは扱いが異なります。[^lit][^transform]

Fragment側の色は、まずシンプルな上下グラデーションにします。

```text
Base Color = Lerp(_RootColor.rgb, _TipColor.rgb, Saturate(UV0.y))
```

### ゲーム側から渡すのは「相手の位置」

C#側は、対象Transformのワールド位置を`_InteractorPositionWS`へ渡し、対象が有効な間だけ`_InteractorEnabled = 1`にします。対象が未設定・破棄済みなら0へ戻し、原点に存在しないキャラクターを作らないようにします。

この基本形は水平距離だけで反応します。高さの違う橋の上のキャラクターにも反応し得るため、複数階のあるゲームでは高さの条件を追加するか、作用する草のグループを分けます。物理衝突、踏み跡の保存、茎の長さを保つ物理シミュレーションではありません。

### Boundsを忘れると、画面端で草が消える

頂点をシェーダで動かす場合は、カリング用のBoundsが変形後の範囲を含むようにします。Unityの`Renderer.localBounds`は、このような用途で上書きできます。ただし、その上書き値はSceneやPrefabへ保存されないため、Runtimeで設定し直す必要があります。[^bounds]

配布メッシュのBoundsを十分広げる方法でも構いません。いずれの場合も、許容する最大変位と最小スケールから範囲を決めます。シェーダの変形はColliderの更新でもありません。

最初の検証では、この草をStatic BatchingやDynamic Batchingの前提にしないでください。特にObject空間へ戻す頂点処理では、まとめ方によって座標の前提が変わらないかを別途確認します。Static／Dynamic Batchingはいずれも、頂点をワールド空間でまとめる処理を含みます。[^batching]

## 06 建物や地形を横断するスキャン波

実装は`Assets/ShaderGraphTechniques/Runtime/06_ScanPulse/`、ローカル確認動画は`Artifacts/Captures/Videos/06_ScanPulse-Perspective.mp4`です。公開URLとGraph Editor画像は未設定です。

探索ゲームのサーチ、ソナー、魔法の索敵などに使える表現です。床だけでなく、壁や段差にも同じ波が走るようにします。

この基本形はポストエフェクトではなく、**対象マテリアルの中で描くスキャン波**です。

### グラフとプロパティ

URPのLit Graphで`Opaque`、`Alpha Clipping = Off`にします。

| Reference | 型・初期値 | 役割 |
| --- | --- | --- |
| `_ScanCenterWS` | Vector3、(0, 0, 0) | 波の中心 |
| `_ScanRadius` | Float、0 | 現在の半径 |
| `_ScanWidth` | Float、0.25 | 光の帯の半幅 |
| `_ScanActive` | Float、0〜1、初期値0 | 波を出すかどうか |
| `_ScanColor` | HDR Color、青緑系 | 波の色 |
| `_ScanIntensity` | Float、3 | 波の明るさ |
| `_BaseColor` | Color、灰色系 | 対象の通常の色 |

### 球面から近い部分だけを光らせる

Fragment側で、`Position / World`と`_ScanCenterWS`の距離を求めます。[^position]

```text
distanceToCenter = Distance(Pws, _ScanCenterWS)
radius           = Max(_ScanRadius, 0)
width            = Max(_ScanWidth, 0.0001)

distanceToShell  = Abs(distanceToCenter - radius)
ring             = (1 - Smoothstep(0, width, distanceToShell))
                   * Saturate(_ScanActive)
```

`distanceToCenter = radius`の球面上で一番明るくなり、そこから内外へ離れるほど暗くなります。`_ScanWidth`は球面の両側へ作用するため、非ゼロになる帯全体の厚みはおおむねその2倍です。

```text
Base Color = _BaseColor.rgb
Emission   = _ScanColor.rgb * _ScanIntensity * ring
Metallic   = 0
Smoothness = 0.25
```

`_ScanRadius`を増やすと、球面と床・壁・建物が交わる場所を発光帯が移動します。XZだけの距離ではなくXYZを使っているので、床に描いた平面の円を壁へ貼り付ける計算とは異なります。

### 複数の物体へ同じ波を渡す

同じ波に参加させるRendererへ、同じ中心、半径、幅を渡します。波が床から壁へ渡る場所でずれていなければ、座標空間をそろえられています。

繰り返し再生では、半径を0へ戻す前に`_ScanActive = 0`にすると、中心で意図しない発光が出るのを避けられます。独立した2つの波を同時に扱う場合は、制御するマテリアルの所有範囲も分けます。

### 「シーン全体に効く」とは説明しない

このGraphを使っていないマテリアルには波が出ません。既存の複雑なLit Graphへ足す場合は、距離から`ring`を返す部分をSub Graphとして移植し、元のEmissionに加算します。

また、通常の深度テストを使うため、手前の壁に隠れた物体まで透視する表現ではありません。「全画面スキャン」や「遮蔽物越しの索敵表示」が必要なら、別の描画設計として扱います。

## 他のプロジェクトへ持ち込むときの考え方

### 見た目の本体と、デモの仕掛けを分ける

ディゾルブのGraphを使いたいだけなのに、撮影用カメラ、説明UI、シーン切り替えManagerまで必要になる構成は避けたいところです。

持ち出しに必要なのは、基本的に**Graph、参照するSub Graph、Material、必要なTexture・Mesh、必要な場合だけRuntimeの制御コンポーネント**です。Prefabは、それらを設定済みの形でまとめる役割にします。

`.shadergraph`や`.mat`だけをエクスプローラーで適当にコピーするのではなく、参照と`.meta`を保つ方法で移します。[^metadata]UnityのExport Packageは依存アセットも含めるための仕組みですが、必要な範囲になっているかは配布前に確認します。[^export]

| 作例 | 見た目を移すときに必要なもの | 移植先で必要な条件 |
| --- | --- | --- |
| ディゾルブ | Graph、Material。自動再生するなら制御も | UV0のあるメッシュ |
| 接触バリア | Graph、Material、球などのメッシュ | Depth Texture、不透明な交差相手 |
| ヒートヘイズ | Graph、時間Sub Graph、Material、Quad | Opaque Texture。基本形は不透明な背景 |
| 積雪 | Graph、Material、岩のTexture | 有効な法線、投影座標とRepeat設定 |
| 草 | Graph、時間Sub Graph、Material、専用Mesh、位置・Bounds制御 | UV0.Yの根元マスク、対象Transform |
| スキャン波 | Graph、Material。自動再生するなら制御も | 波を出す対象すべてへのパラメーター設定 |

この構成で6作例別とRuntime-Allの`.unitypackage`を生成し、アーカイブを展開して収録path／GUID／SHA-256と依存境界を検査しました。

### マテリアルの共有は、意図して行う

2つのPrefabを置いたとき、片方のProgressを変えたらもう片方まで消えるのでは困ります。

再生中に個別変更する値は、所有関係を決めたマテリアルインスタンスか、適切に管理したMaterialPropertyBlockで渡します。共有のMaterialアセットをそのまま書き換える設計にしないでください。

なお、MaterialPropertyBlockは「使えば必ず最適化される道具」ではありません。Unityの公式APIはSRP Batcherと互換でないことを明記しています。個別制御のしやすさと、CPU側の描画コストは分けて判断します。[^mpb]

### 公開前の最も大切な試験

サンプルプロジェクトの中で動くことに加え、**新しく作ったUnity 6.3のURPプロジェクトへ、作例1つ分だけをImportして動くこと**を確認しました。

個別6 PackageとRuntime-Allを、元プロジェクトのAssets／Libraryを持ち込まない7個の独立URPプロジェクトへImportしました。Gallery Sceneやデモ用Managerなしで、Prefab／Graph／Shader、制御した状態A/Bの実レンダー、不足スクリプト0、マゼンタFallbackなしを確認しています。結果と画像ハッシュは`Documentation/VALIDATION.md`に記録しました。

描画負荷については、対象GPU、解像度、Graphics API、表示数をそろえて測定します。ノード数の少なさだけで「軽量」と断定しません。特に背景コピー、深度取得、透明面の重なり、Triplanarの複数サンプルは、それぞれ異なる負荷の要因になります。

## まとめ

6つの表現を別々の小技として覚えるより、使った入力に注目すると応用しやすくなります。

ディゾルブと積雪は、値から**見せる領域**を作る表現です。バリアとヒートヘイズは、カメラが持つ**画面の情報**を読む表現です。草とスキャン波は、**位置や距離**を使って変化を作ります。

どの作例でも、最後に確認したいのは「動画で映えたか」だけではありません。**別のメッシュ、別の位置、別のプロジェクトへ移したとき、入力と前提が説明できるか。**そこまでそろっていると、自分のゲームへ持ち込みやすいShader Graphになります。

今回のサンプルでは、構造検査68/68、EditMode 9/9、PlayMode 4/4、StrictMode Windows Playerビルド0 errors／0 warnings、Package内容検査7/7、隔離移植7/7を完了しました。実映像はGallery＋6作例Perspectiveと、バリア／ヒートヘイズのOrthographicを生成済みです。一方、XR、モバイル、別OS／GPU／Graphics API、実時間60fps性能、公開Git URL、YouTube URLは未検証または未作成です。

[^unity63]: Unity公式：[New in Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)。本稿は2026年9月12日に参照。
[^urp-asset]: Unity公式：[Universal Render Pipeline asset reference for URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/universalrp-asset.html)。Depth Texture、Opaque Texture、Opaque Downsamplingなど。
[^lit]: Unity公式：[Lit shader graph reference for URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/prebuilt-shader-graphs-urp-lit.html)。Vertex／Fragment入力、Graph設定。
[^unlit]: Unity公式：[Unlit shader graph reference for URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/prebuilt-shader-graphs-urp-unlit.html)。透明ブレンド、Render Face、Depth Write／Testなど。
[^bloom]: Unity公式：[Bloom in URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/post-processing-bloom.html)。
[^camera]: Unity公式：[Camera component reference for URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/camera-component-reference.html)。カメラのPost ProcessingとVolume関連設定。
[^post]: Unity公式：[Post-processing in URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/integration-with-post-processing.html)。
[^step]: Unity公式：[Step Node / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Step-Node.html)。
[^smoothstep]: Unity公式：[Smoothstep Node / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Smoothstep-Node.html)。
[^position]: Unity公式：[Position Node / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Position-Node.html)。
[^transform]: Unity公式：[Transform Node / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Transform-Node.html)。座標空間、Position／Direction／Normalの違い。
[^properties]: Unity公式：[Property Types / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Property-Types.html)。Reference、Show In Inspectorなど。
[^time]: Unity公式：[Time Node / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Time-Node.html)。
[^noise]: Unity公式：[Simple Noise Node / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Simple-Noise-Node.html)。
[^depth-difference]: Unity公式：[Scene Depth Difference node / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Scene-Depth-Difference-Node.html)。Eyeモード、入力座標、差の符号。
[^fresnel]: Unity公式：[Fresnel Effect Node / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Fresnel-Effect-Node.html)。
[^screen-position]: Unity公式：[Screen Position Node / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Screen-Position-Node.html)。
[^scene-color]: Unity公式：[Scene Color node / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Scene-Color-Node.html)。Opaque Texture、実行段階、描画順の条件。
[^triplanar]: Unity公式：[Triplanar Node / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Triplanar-Node.html)。3方向の投影、3回のサンプル、Input Space。
[^normal]: Unity公式：[Normal Vector Node / Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Normal-Vector-Node.html)。
[^bounds]: Unity公式：[Renderer.localBounds / Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Renderer-localBounds.html)。シェーダ変形向けのBoundsと保存に関する制約。
[^export]: Unity公式：[Create asset packages](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetPackagesCreate.html)、[AssetDatabase.ExportPackage](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.ExportPackage.html)。
[^mpb]: Unity公式：[MaterialPropertyBlock / Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MaterialPropertyBlock.html)。SRP Batcherとの互換性についての注意。

[^texture-import]: Unity公式：[Default texture type reference](https://docs.unity3d.com/6000.3/Documentation/Manual/texture-type-default.html)。sRGB、Mip Maps、Wrap Mode。
[^batching]: Unity公式：[Introduction to batching meshes](https://docs.unity3d.com/6000.3/Documentation/Manual/DrawCallBatching.html)。Static／Dynamic Batchingとワールド空間。
[^metadata]: Unity公式：[Asset metadata](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html)。アセット識別子・インポート設定と`.meta`の保持。
