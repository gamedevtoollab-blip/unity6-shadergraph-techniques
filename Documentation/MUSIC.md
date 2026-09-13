# 公開用動画のBGM

## 採用音源

| 項目 | 内容 |
|---|---|
| 曲名 | `Digital Clouds` |
| 作者 | Alejandro Magaña (A. M.) |
| 配布元 | Mixkit |
| 掲載ページ | <https://mixkit.co/free-stock-music/electronic/> |
| 原音源 | <https://assets.mixkit.co/music/175/175.mp3> |
| ライセンス | Mixkit Stock Music Free License: <https://mixkit.co/license/#musicFree> |
| 原音源SHA-256 | `71cd4ea39edcc7532672bd97311abadfd318d00e7a828310a88b4f57fad9cd48` |
| 確認日 | 2026-09-13 |

Mixkitの公式案内では、Stock MusicはYouTube、SNS、オンライン広告を含む動画で無料利用でき、クレジットは必須ではありません。公開時点の利用条件は必ず上記公式ページで再確認してください。

任意でクレジットを記載する場合は、次を利用できます。

```text
Music: "Digital Clouds" — Alejandro Magaña (A. M.), via Mixkit
```

## 組み込み方法

原音源は`Artifacts/SourceAudio/Mixkit-Digital-Clouds-175.mp3`へ保存します。これは最終動画を再生成するためのローカル入力であり、原音源単体を配布Packageや公開リポジトリへ含めません。

`Tools/Build-ShowcaseVideo.ps1`は冒頭から65秒を使用し、0.8秒のフェードイン、終端3秒のフェードアウト、`-16 LUFS`／True Peak `-1.5 dBFS`目標のラウドネス処理を行い、48kHzステレオAAC 192kbpsとしてMP4へ埋め込みます。最終ファイルの実測値は`Artifacts/Validation/Showcase/showcase-video-validation.json`に記録します。
