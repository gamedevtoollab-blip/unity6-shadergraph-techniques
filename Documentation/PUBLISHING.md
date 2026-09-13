# 公開前の手順

この文書は、ローカルで完成・検証済みの成果物を公開する際の残作業を定義します。GitHubソース公開先は`https://github.com/gamedevtoollab-blip/unity6-shadergraph-techniques`です。YouTubeへのアップロード、生成物のRelease添付、Zennへの公開は実施していません。

## 1. 公開主体を確定する

- 公開リポジトリは`gamedevtoollab-blip/unity6-shadergraph-techniques`です。
- 公開用のGit author／committerは`gamedevtoollab-blip <gamedevtoollab@gmail.com>`を、このリポジトリのlocal configだけに設定します。
- LICENSEの著作権表示を変更する必要がある場合は、公開者の指示を得てから変更する。

## 2. ソース履歴を作る

1. `git status --short --ignored`で、`Assets/`、`Packages/`、`ProjectSettings/`、`Documentation/`が追跡候補であることを確認する。
2. `Library/`、`Logs/`、`UserSettings/`、`Artifacts/`がignoreされていることを確認する。
3. 秘密情報、ローカル絶対パス、Unity CLIの認証情報を含むログが追跡対象にないことを再監査する。
4. 確認済みの公開者情報でcommitを作成し、そのcommit SHAを記録する。
5. 指定されたremoteだけを追加してpushする。remote URLや公開先を推測しない。

`Artifacts/`は再生成可能なPlayer、動画、Package、移植試験Project、検証ログを含むため、ソース履歴には含めません。配布する7個の`.unitypackage`と必要な映像は、公開先のRelease添付などへ別途配置します。

## 3. 動画を公開する

- 公開用の`ShaderGraphTechniques-YouTube-Showcase-Final.mp4`と個別技術確認9本を、`Documentation/CAPTURE.md`のSHA-256と照合する。
- 公開用単一映像を記事冒頭で全体像を示す動画として使い、各作例は同じ動画の開始時刻へリンクする。個別Perspective／Orthographic動画は技術確認が必要な場合だけ使う。
- `Documentation/MUSIC.md`でBGMの最新利用条件を再確認する。クレジットは必須ではないが、記載する場合は同文書の文面を使う。
- YouTube側で実際に再生し、解像度、アスペクト比、開始・終了、BGMの音量、Content IDの判定を確認する。
- 動画IDと実際のタイムコードを記録する。予定URLや仮タイムコードを記事へ入れない。

## 4. 記事を公開用に仕上げる

対象は`Documentation/unity63-shadergraph-six-techniques.md`です。

1. 冒頭の`PUBLICATION`コメント2件を、実在するGit URL、固定commit SHA、YouTube URLがそろってから置き換える。
2. 各作例冒頭のローカルパス説明へ、公開ソースの固定commit URLと実動画URLを追加する。
3. Gallery動画を記事冒頭へ1件だけ埋め込み、各作例は必要な表示範囲へ直接移動できる実タイムコード付きリンクにする。
4. Graph Editor画像を掲載する場合は、対象Graph、Unity版、撮影時点をそろえ、本文の接続と画像が一致することを確認する。
5. `published: false`のまま最終プレビューを確認し、公開操作は別途明示された依頼の範囲で行う。

## 5. 公開直前の再確認

- `Documentation/VALIDATION.md`に記載した検証対象と、公開するcommitの内容が同一である。
- PackageのSHA-256が`Documentation/DISTRIBUTION.md`と一致する。
- 動画のSHA-256が`Documentation/CAPTURE.md`と一致する。
- 記事内の公開リンクがすべて到達し、固定commitのファイルへ向いている。
- XR、モバイル、別OS／GPU／Graphics API、実時間60fps性能を検証済みと誤記していない。
