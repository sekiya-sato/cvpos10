# aicoding_log (cvpos10)

AI エージェントによる作業ログ。新しい作業を **先頭に挿入** する。
書式は `AGENTS.md` の "Log-Format" を参照。

---

## [2026-07-31] 13:10 TM-m30IIプリンタの接続タイミングを売上開始時に変更
### Agent
- [Claude Opus 5 : Anthropic]
### Editor
- [ClaudeCode]
### 目的
- ユーザーからの要望：TM-m30 プリンタは売上開始時に接続し、接続エラーがあればその時点で表示する。
### 実施内容
- ViewModels/06Uriage/PosUriageInputViewModel.cs: `Init`（画面表示時）でのプリンタ接続をやめ、DM-D30 のみ接続するよう変更。`ScanBarcode` で明細が 0 件から 1 件目を積んだとき（＝売上開始）に `ConnectPrinterOnSaleStartAsync` を呼ぶよう追加。接続失敗時はその場でステータスに「TM-m30II 接続エラー: … ／このままではレシートを印字できません。接続ボタンで再試行してください。」を表示する。
- ViewModels/06Uriage/PosUriageInputViewModel.cs: 手動接続の `ConnectPrinterCommand` を async 化。
- Services/PosPeripheralService.cs: 接続状態を照会する `IsDisplayOpen` / `IsPrinterOpen` を追加。
### 技術決定 Why
- 接続タイミングを会計時ではなく売上開始時にしたのは、会計確定後の印字で初めて接続不良に気付くと、売上だけ登録されてレシートが出せない状態になるため。1 件目の読取時点で判明すれば、客を待たせる前に復旧できる。
- 2 件目以降の読取では `IsPrinterOpen` で判定して再接続しない（毎回 Dispose→Open すると読取が遅くなるため）。
- Bluetooth 仮想 COM の `SerialPort.Open()` は数秒ブロックすることがあるため、`Task.Run` で UI スレッドから外した。
### 確認
- `dotnet build cvpos10.slnx` 成功（0 警告 / 0 エラー）。
- 起動確認：売上入力画面の表示 OK。実機（COM6 / TM-m30II）での接続確認は未実施。

---

## [2026-07-31] 12:35 ログイン後の売上入力遷移修正とMaterialDesign再構成
### Agent
- [Claude Opus 5 : Anthropic]
### Editor
- [ClaudeCode]
### 目的
- ユーザーからの要望：ログイン後そのまま売上入力へ遷移させる。あわせて cv10/CvWpfclient を参考に MaterialDesign 対応・デザイン統一を行い、cvpos10 を再構成する。
### 実施内容
- App.xaml.cs: 起動処理を再構成。`ShutdownMode` を `OnExplicitShutdown` → 売上入力表示時に `OnMainWindowClose` へ切替。保存済み JWT のリフレッシュ成功時はログイン画面を出さずに売上入力へ直行。
- App.xaml: MaterialDesign `BundledTheme`(Light/DeepPurple/Green) + `MaterialDesign3.Defaults` + 共通 ResourceDictionary をマージ。Converter を集約。
- AppGlobal.cs: 新規。設定 / gRPC クライアント / 周辺機器サービスを遅延初期化で共有保持。
- cvpos10.csproj: MaterialDesignThemes・MaterialDesignColors・Microsoft.Xaml.Behaviors.Wpf を追加。`RootNamespace` を `CvPos10` に変更。
- Resources/UIColors.xaml, UICommon.xaml, UIFormStyles.xaml: CvWpfclient から同一キーで移植（POS で未使用の検索テキストボックス系は除外）。
- Resources/UIPos.xaml: 新規。POS 固有スタイル（バーコード欄・金額欄・合計パネル・主要操作ボタン）。
- Helpers/Windows/BaseWindow.cs: 新規。CvWpfclient の BaseWindow の POS 向け簡易版（ESC / InitCommand / ExitCommand / 最小サイズ / 表示位置補正）。
- Helpers/Behaviors/{FocusHelper,PasswordBoxAssistant}.cs, Helpers/Converters/{InverseBoolean,StringIsNotEmptyToVisibility}Converter.cs: CvWpfclient から移植。
- Views/00System/LoginView.xaml(.cs): LoginWindow を置換。ColorZone ヘッダー + FloatingHint 入力 + 進捗バー。
- Views/06Uriage/PosUriageInputView.xaml(.cs): MainWindow を置換。ColorZone ヘッダー + Card + MaterialDesignDataGrid + DialogHost による会計画面。F12 で会計。
- ViewModels/00System/LoginViewModel.cs: `Password` → `LoginPassword`、`IsCancelled` と `ExitCommand` を追加。認証成功時に `AppGlobal.LoginId` を設定。
- ViewModels/06Uriage/PosUriageInputViewModel.cs: `PosViewModel` から改名。`InitCommand`（周辺機器の自動接続）と `ExitCommand`（会計中は明細へ戻る）を追加。
- 配置換え: Devices/ (Dmd30DirectController, EpsonTmM30IiPrinter)、Models/ (ReceiptData)、ViewModels/Views を業務分類フォルダへ。名前空間を `Dmd30CustomerDisplay` → `CvPos10` に統一。
### 技術決定 Why
- 遷移しない原因は `ShutdownMode` だった。ログインダイアログを閉じた時点でウィンドウ数が 0 になり、既定の `OnLastWindowClose` によりアプリが終了していた（`ShutdownMode` 指定を外した状態で再現。認証成功後に終了コード 0 でプロセスが落ちることを実測）。売上入力を `MainWindow` に設定するまでを `OnExplicitShutdown` で保護する方式を採用。
- ViewModel は XAML の `DataContext` で宣言する CvWpfclient の書き方に合わせるため、`AppGlobal` を遅延初期化にして引数なしコンストラクタから共有インスタンスを取得できるようにした（チャネル生成だけでは通信しないためデザイナ表示でも副作用なし）。
- 会計画面は独自オーバーレイをやめて `materialDesign:DialogHost` に置換。ESC の扱いを BaseWindow 側の `ExitCommand` に一本化できるため。
- スタイルはキー名を CvWpfclient と揃えて移植し、POS 固有分のみ `UIPos.xaml` に分離した。将来 CvWpfclient 側の変更を取り込みやすくするため。
### 影響範囲
- cvpos10 プロジェクト全体（名前空間・フォルダ構成・全 View）。cv10 側のファイルは変更していない。
### 確認
- `dotnet build cvpos10.slnx` 成功（0 警告 / 0 エラー）。
- 起動確認：ログイン画面（CV POS ログイン）表示 OK。
- 認証成功を差し込んだ検証で、ログイン → 「売上入力」ウィンドウ表示 → 常駐継続を確認。
- 売上入力画面・会計ダイアログのスクリーンショットで MaterialDesign 適用を目視確認。

---
