# 配布と移植

## 生成済みPackage

`Artifacts/Packages/`に6作例別とRuntime全体の7 Packageを生成済みです。

| Package | bytes | Assets | SHA-256 |
|---|---:|---:|---|
| `ShaderGraphTechniques-01-Dissolve.unitypackage` | 33,713 | 8 | `9beda8c18d6b580516576d835cc4e96603d834952cf005555b9107c9956bc36f` |
| `ShaderGraphTechniques-02-IntersectionShield.unitypackage` | 26,981 | 5 | `9d806b4d32ba4e9127f7256cbab529bed5f6eac36d47bfe4e48ede6d8e2803c2` |
| `ShaderGraphTechniques-03-HeatHaze.unitypackage` | 25,850 | 9 | `3d39317187a03676bfdad2bf1913a4cc4308b82acad2829e1ffda540ca4c1e58` |
| `ShaderGraphTechniques-04-TriplanarSnow.unitypackage` | 139,785 | 6 | `a6aaffe8a60be0904c13655aef28cfb9cc3a4ef9b787c9a393b3d7b331f025f5` |
| `ShaderGraphTechniques-05-InteractiveGrass.unitypackage` | 62,047 | 10 | `45dfa63432f64010a5fe807f879b4887f30f903f60ca7a7bb6f2dee919edfb91` |
| `ShaderGraphTechniques-06-ScanPulse.unitypackage` | 29,330 | 8 | `da3ba857a284f882080368b556383eb5869a6bfe4f938f84acb5b000747835d1` |
| `ShaderGraphTechniques-Runtime-All.unitypackage` | 305,142 | 34 | `3b50115a593aa27924137f0ddaf328f3b4e671adcecb33870d992c01250ceccf` |

`package-manifest.json`に全Asset path、GUID、asset SHA-256を保存しています。作例別Packageは、その作例フォルダーと実際の参照先だけを閉包化しています。Demo、Tests、Editor、ProjectSettings、別作例は含みません。MITライセンスのAsset内コピーと作例別`GETTING_STARTED_JA.md`を明示的に含めています。

各アーカイブを実際に展開してマニフェストと照合し、7/7 PASSしました。共通LICENSEのGUIDはすべて`35b705c95a276624ebe82a60b7ebd085`です。詳細は`Artifacts/Validation/package-content-validation.json`を参照してください。

## 作例1つだけを移植する

1. Unity `6000.3.22f1`のUniversal 3Dプロジェクトを開き、URP／Shader Graph `17.3.0`であることを確認します。
2. 対象の`.unitypackage`だけをImportします。
3. 02 Intersection ShieldではURP AssetとCameraのDepth Texture、03 Heat HazeではOpaque Textureを有効にします。ほか4作例は作例計算のための追加Texture設定を要求しません。
4. `Assets/ShaderGraphTechniques/Runtime/<作例>/PF_*.prefab`をSceneへ置くか、`MAT_*.mat`を自分のRendererへ割り当てます。
5. 同梱`GETTING_STARTED_JA.md`のRuntime API、Mesh条件、既知の制約を確認します。

01／03／05／06には必要最小限の制御コンポーネントが含まれます。02／04はMaterialプロパティを直接変更できます。Runtime PrefabはCamera、Light、Volume、UI、Demo Manager、固定Scene名、`Resources`参照を要求しません。

## 隔離移植の実績

元プロジェクトのAssets／Libraryを持ち込まず、7個の新規Unity 6.3 URPプロジェクトに個別6 PackageとRuntime-AllをImportしました。7/7 PASSです。各環境でPrefab、Graph、Shaderコンパイル、不足スクリプト0、制御した状態A/Bの実レンダー、画像差分を確認しています。Runtime-All環境では6作例すべてを同一Import後にレンダーしました。

試験Projectは`Artifacts/MigrationProjects/`、集約証跡は`Artifacts/Validation/migration-validation-summary.json`です。これらはローカル検証用で、公開ソース履歴へは含めません。

別の作例を後からImportしても、Commonファイルは同じ元GUIDを共有します。Packageの再生成はUnityメニュー`Tools/Shader Graph Techniques/Export Runtime Packages`または`ShaderGraphTechniquePackageExporter.ExportAll`から行えます。
