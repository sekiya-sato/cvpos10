# AGENTS.md - cvpos10 AI Agent Instructions

cv10 (`creativevision10`) の `AGENTS.md` から、cvpos10 に必要な項目のみを抜粋・調整したもの。

## Tooling & Environment
- **Stack**: .NET 10, C# 14, gRPC (protobuf-net.Grpc), WPF (MVVM, CommunityToolkit.Mvvm), MaterialDesignInXamlToolkit 5.3.2
- **Files**: Solution `cvpos10.slnx` / Project `cvpos10/cvpos10.csproj`
- **Line Endings**: 編集・作成するすべてのファイルは **CR+LF (`\r\n`)**。LF/CR の混在は禁止。
- **Encoding**: **UTF-8**

## Priority Workflow (IMPORTANT)
**Analyze → Plan (TODO-LIST) → Execute → Verify → Write-Log → Git-Commit**
- Language: 計画・説明・コメントは **日本語**。
- Task Mgmt: `in_progress` のタスクは常に 1 つだけ。
- Search: 日本語の語句を探すときは `grep -r` を使う。

## Architecture
- **依存関係**: `cvpos10` → `cv10/CodeShare`(Layer 0) / `cv10/CvAsset`(Layer 0) をプロジェクト参照。
- **cv10 側のファイルは変更しない**（`C:\gitroot\new2022\cv10\` 配下は読み取り専用として扱う）。
  共有コードの修正が必要な場合は、cvpos10 側で完結させるか、ユーザーに確認する。
- サーバとの通信は `CvAsset` のサービス契約 (`IPointOfSaleService` / `ILoginService`) 経由。
  認証は JWT。`Authorization: Bearer` ヘッダを `PosGrpcClient` が付与する。

## プロジェクト構成
`cv10/CvWpfclient` のフォルダ規約に合わせている。新規ファイルも同じ場所に置くこと。

```
cvpos10/
  App.xaml(.cs)        起動処理（ログイン → 売上入力への遷移）
  AppGlobal.cs         設定 / gRPCクライアント / 周辺機器サービスの共有インスタンス
  Devices/             シリアル接続の周辺機器 (DM-D30, TM-m30II)
  Models/              データモデル (ReceiptData 等)
  Services/            Pos* サービス（通信・設定・トークン保存・周辺機器）
  Helpers/
    Windows/           BaseWindow
    Behaviors/         添付プロパティ (FocusHelper, PasswordBoxAssistant)
    Converters/        IValueConverter
  Resources/           UIColors / UICommon / UIFormStyles / UIPos
  ViewModels/00System, 06Uriage/    業務分類フォルダは CvWpfclient と同じ番号体系
  Views/00System, 06Uriage/
```

## Coding & WPF Standards
- **C#**: file-scoped namespace、Allman ブレース、インデントは **半角スペース 4**。
- **XAML**: cv10 の `Settings.XamlStyler` に準拠（**タブ**インデント、属性の並び順、`Margin` は `ThicknessSeparator=2`）。
- **WPF Work**: UI の不具合は、まず `App.xaml` と `Resources/*.xaml` の ResourceDictionary を確認する。
- **スタイルの追加場所**:
  - CvWpfclient と共通の見た目 → `Resources/UIFormStyles.xaml`（キー名も CvWpfclient と揃える）
  - POS 固有の見た目 → `Resources/UIPos.xaml`
  - 色は `Resources/UIColors.xaml` に集約し、View に直接カラーコードを書かない。
- **Window**: 画面は `helpers:BaseWindow` を継承する。ESC 終了・`InitCommand`・`ExitCommand`・
  最小サイズ・表示位置補正が共通で効く。
- ViewModel は XAML の `DataContext` で宣言できるよう、`AppGlobal` を使う引数なしコンストラクタを用意する。
- 過度な DI は避ける。
- 指示がない限りテストプログラムを追加しない。

## Build & Run
```
dotnet build C:\gitroot\new2022\cvpos10\cvpos10.slnx
```
- 実行ファイル: `cvpos10/bin/Debug/net10.0-windows/cvpos10.exe`
- 接続先・店舗・端末の設定は `cvpos10/appsettings.json`（`ServerUrl` / `StoreId` / `WarehouseId` /
  `StaffId` / COM ポート）。JWT は `%LOCALAPPDATA%\CVPOS\clientsettings.json` に保存される。
- XAML のリソースキー解決はビルドでは検出できないため、UI を変更したら必ず起動して確認する。

## Post-Task Requirements (Log & Commit)
- **Log**: `Doc/aicoding_log.md` に追記する。800 行を超えたら `aicoding_log_[NNN].md` へアーカイブ。
- **Log Format**: 下記 "Log-Format" に従い、**先頭に挿入**する。
- **Commit Format**: 下記 "Commit-Format" に従う。

### Log-Format
```
## [YYYY-MM-DD] hh:mm 作業タイトル
### Agent
- [使用した AI Model 名 : AI Provider 名]
### Editor
- [使用したエディタ 例 "VS2026", "VSCode", "ClaudeCode"]
### 目的
- ユーザーからの要望：[内容端的に]
### 実施内容
- [ファイル名]: [変更内容の要約]
### 技術決定 Why
- [なぜその方法を選んだか]
### 影響範囲 (省略可)
- 大規模変更の場合は影響範囲を明記。修正したファイルのみの場合は省略
### 確認
- [Build 結果や起動確認などを簡潔に記述]

---
```

### Commit-Format
```
[作業内容]
[使用した AI Model 名 : AI Provider 名 : エージェント名]
[開始時間] - [終了時間] : [作業時間] (**日本時間JSTで記録**)
[ユーザ指示の概略]
```
例)
```
LoginView.xamlのMaterialDesignスタイルへの変更
GPT-5.4-mini : OpenAI : Build
16:00 - 17:30 : 1時間30分
LoginView のデザインを CvWpfclient のデザインに統一する
```
