
## [2026-08-01] 17:43 POS フル機能実装 Phase 3: メニュー画面 + 起動フロー変更
### Agent
- kimi-k3 : opencode-go
### Editor
- OpenCode
### 目的
- 初期画面をメニュー化し、売上入力/精算入力/各種レポート/レシート一覧/再ログイン/終了のボタンを配置
### 実施内容
- cvpos10/Views/MenuView.xaml(.cs) (新規): BaseWindow 継承、ColorZone ヘッダー、UniformGrid の 6 ボタン、フッター（店舗名/ログインID/日時）
- cvpos10/ViewModels/MenuViewModel.cs (新規): OpenSales/ReLogin/Exit コマンド。未実装ボタンは IsEnabled=false + ToolTip
- cvpos10/Resources/UIPos.xaml: PosMenuButton スタイル追加（MaterialDesignRaisedButton 継承、FontSize=20、MinHeight=72）
- cvpos10/App.xaml.cs: ShowUriageInput() → ShowMenu() に変更。using 整理
- cvpos10/ViewModels/06Uriage/PosUriageInputViewModel.cs: Dispose から AppGlobal.Shutdown() を除去（メニュー復帰後に gRPC クライアントが生存するため）
### 技術決定 Why
- MenuView を MainWindow にして ShutdownMode=OnMainWindowClose とし、各機能は ShowDialog で開く（cvpos32 のナビゲーション方式に合わせる）
- PosUriageInputViewModel.Dispose で AppGlobal.Shutdown() を呼んでいたため、売上画面を閉じるたびに gRPC チャネルが破棄されていた。App.OnExit に統合して多重起動を防ぐ
### 確認
- cvpos10: ビルド 0 警告 0 エラー。exe 起動確認: プロセスが即座に終了せず生存（ログイン画面表示まで到達）

---

## [2026-08-01] 17:25 POS フル機能実装 Phase 1-2: サーバ拡張 + クライアント基盤
### Agent
- kimi-k3 : opencode-go
### Editor
- OpenCode
### 目的
- ユーザーからの要望：cvpos32 を参考に cvpos10 の初期画面をメニューとし、ログイン・売上入力・精算入力・各種レポート・レシート一覧などを備えた POS フル機能を実装（オンライン前提、マスタ DL/UL 不要）
### 実施内容
- cv10/CodeShare/IPointOfSaleService.cs: PosCheckoutRequest/Line に Kubun/StaffId/StaffCode/StaffName を追加、CancelSaleAsync/SaveSeisanAsync RPC を新設
- cv10/CvBase/BaseDb3Pos.cs (新規): Tran02PosSeisan エンティティ（POS 日次精算テーブル）を定義
- cv10/CvBase/DefineDataTable.cs: Tran02PosSeisan を tableTypes に追加（サーバ起動時自動 CreateTable）
- cv10/CvServer/Services/PointOfSaleService.cs: 返品（Kubun=20 対応・在庫自動戻し）、取消（PosClientSaleId+":C" で取消伝票生成）、SaveSeisanAsync（金種算出・SeisanCnt インクリメント）を実装
- cvpos10/cvpos10.csproj: CvBase ProjectReference を追加
- cvpos10/Services/PosGrpcClient.cs: ICoreService チャネル生成、QueryListAsync<T>（汎用照会）/CancelSaleAsync/SaveSeisanAsync のクライアントラッパを追加
### 技術決定 Why
- 照会系は ICoreService+QueryListParam のみ（任意 SQL 禁止）で cv10 変更を最小化。書込み系は型付き RPC でサーバ側で一貫したバリデーション・在庫連動を実現
- Tran02PosSeisan は DefineDataTable の既存 CreateTable 仕組みに 1 行追加するだけでサーバ起動時に自動作成されるため、マイグレーション不要
- クライアント側の集計（レポート・精算・ジャーナル）でサーバ負荷を抑えつつ、既存 Tran01Tenuri のみで照会完結
### 影響範囲
- cv10 側: 4 ファイルのみ変更。他セッションの未コミット変更（CvWpfclient/*）には一切触れない
### 確認
- CvServer: ビルド 0 警告 0 エラー
- cvpos10: ビルド 0 警告 0 エラー（CvBase 参照追加後も Newtonsoft.Json は CvBase から推移的に解決）

---
# aicoding_log (cvpos10)

AI エージェントによる作業ログ。新しい作業を **先頭に挿入** する。
書式は `AGENTS.md` の "Log-Format" を参照。

---

## [2026-07-31] 14:13 TM-m30IIのShift_JIS印字と円記号を再修正
### Agent
- [GPT-5 : OpenAI]
### Editor
- [Codex]
### 目的
- ユーザーからの要望：前回の修正後も残る半角文字化けを解消し、半角の円記号をバックスラッシュでなく `¥` として印字する。
### 実施内容
- Devices/EscPosBuilder.cs: Shift_JIS体系で不要な `FS &` / `FS .` の挿入を撤去。`ESC @` の後に `FS C 1`（Shift_JIS）と `ESC R 8`（日本）を設定して、CP932バイト列をそのまま送るように変更。
- Devices/EpsonTmM30IiPrinter.cs: 接続時の初期化にも `ESC R 8` を追加。
- .agents/skills/epson-escpos-research/SKILL.md: Shift_JISでの自動2バイト処理、JIS用の漢字モードとの使い分け、円記号の日本文字セット選択を追記。
### 技術決定 Why
- `FS C 1` はShift_JIS体系を選択し、漢字の先頭バイトを検出すると次のバイトを自動的に漢字の第2バイトとして処理する。`FS &` / `FS .` はJIS体系でのみ必要なため、Shift_JISデータ中へ混在させると文字化けを起こす。
- CP932の半角 `¥` は0x5Cで送られる。`ESC R 8` の日本国際文字セットを選択して、同じ0x5Cをプリンタ上で円記号として印字する。
### 確認
- 生成する初期化バイト列を `1B 40 1C 43 01 1B 52 08` として静的確認。
- 実機での再印字は次の接続後に確認する。

---

## [2026-07-31] 13:59 TM-m30IIの文字コード・用紙幅切替・通信速度設定の修正
### Agent
- [GPT-5 : OpenAI]
### Editor
- [Codex]
### 目的
- ユーザーからの要望：TM-m30IIの印字で半角文字が化ける問題を解消し、appsettings.json の用紙幅（58 / 80）で本体設定も切り替える。BaudRateを設定ファイルから削除して機器側に固定し、ESC/POS調査手順をスキル化する。
### 実施内容
- Devices/EscPosBuilder.cs: CP932の2バイト文字だけを `FS &` / `FS .` で囲むよう変更。半角英数・記号・半角カナは通常モードで送るようにした。
- Devices/EpsonTmM30IiPrinter.cs: `GS ( E` Function 6で現在の紙幅を照会し、`PaperWidthMm` と異なる場合だけFunction 1 → 5（a=3、58mm=2 / 80mm=6）→ 2でTM-m30II本体の設定を更新するよう追加。設定モード開始通知を確認し、リセット後に初期化する。
- Devices/PosPaperWidth.cs: TM-m30IIの紙幅カスタム値への変換を追加。
- Devices/Dmd30DirectController.cs, Devices/EpsonTmM30IiPrinter.cs, Services/PosPeripheralService.cs, Services/PosSettings.cs, appsettings.json: Display / Printer のBaudRateをappsettings.jsonから削除し、DM-D30=19200、TM-m30II=115200として各デバイス実装内に固定した。
- .agents/skills/epson-escpos-research/SKILL.md: EPSON公式リファレンスの調査URL、検索式、文字コード・用紙幅設定の確認手順を追加。
### 技術決定 Why
- `FS &` はJIS漢字モードを選択し、その間の文字列を2バイトとして処理する。従来は行全体を囲んでいたため、半角文字が2バイト文字として解釈されていた。全角文字の連続範囲だけを切り替えることで、半角と日本語を同じ行で正しく併用する。
- 紙幅設定は不揮発メモリへ書き込むため、Function 6の読出し結果が設定値と異なる場合だけFunction 5で更新し、過剰な書込みを避ける。
### 確認
- `dotnet build cvpos10.slnx -p:BaseOutputPath=...\\.omo\\build_verify\\` 成功（Debug、0警告 / 0エラー）。起動中の実行ファイルをロックしない隔離出力先で確認した。
- `git diff --check` 成功。半角文字を行全体の漢字モードへ送る旧パターンがないこと、58mm=2 / 80mm=6の設定バイト列を静的確認。
- 調査スキルのfrontmatterを検証。`quick_validate.py` はバンドルPythonにPyYAMLがないため実行不可だった。
- 実機での再印字・設定読出しは未実施。

---

## [2026-07-31] 13:20 レシート・領収書の様式対応と58mm/80mm用紙幅の自動判定
### Agent
- [Claude Opus 5 : Anthropic]
### Editor
- [ClaudeCode]
### 目的
- ユーザーからの要望：添付見本と同じ様式のレシートと領収書（領収書ボタンを追加し、必要なときのみ印字）を印字可能にする。58mm / 80mm の 2 種類の用紙幅にそれぞれ対応し、どちらも幅をできるだけ使って表示する。
### 実施内容
- Devices/PosPaperWidth.cs: 新規。用紙幅（58mm=30桁 / 80mm=48桁、Font A 換算）とバーコードのモジュール幅を定義。
- Devices/EscPosBuilder.cs: 新規。ESC/POS コマンド列のビルダ。Shift_JIS のバイト数を桁数として左右振り分け・右寄せ・区切り線・折り返しを行い、倍角中は有効桁数を半分として扱う。CODE39 バーコード出力も持つ。
- Devices/ReceiptDocumentBuilder.cs: 新規。ReceiptData からお買上げレシートと領収書のコマンド列を組み立てる。桁数は用紙幅から決まるため、同じコードで 58mm / 80mm の双方が幅いっぱいになる。
- Devices/EpsonTmM30IiPrinter.cs: Open 時に `GS ( E` Function 6 / カスタム値 a=3（1D 28 45 02 00 06 03）で用紙幅を問い合わせ、応答 `37 27 33 1F <値> 00` の値 "2"=58mm / "6"=80mm を解釈するよう変更。応答なし・未知の値のときは appsettings.json の PaperWidthMm を使う。PrintTaxInvoice（領収書）を追加。SerialPort に ReadTimeout を追加。
- Models/ReceiptData.cs: 店舗情報（名称・住所・電話）、販売員コード、明細（商品コード・商品名・カラーサイズ・JAN）、税抜小計・消費税・税込合計、アプリバージョンを持つよう拡張。
- Services/PosSettings.cs: StoreAddress / StorePhone / StaffCode / TaxRatePercent / PaperWidthMm を追加。ResolvedStaffCode（未設定時は StaffId の 6 桁ゼロ埋め）を追加。
- Services/PosPeripheralService.cs: PrintTaxInvoiceAsync と PaperWidth / IsPaperWidthDetected を追加。
- ViewModels/06Uriage/PosUriageInputViewModel.cs: SubTotal（税抜小計）/ TaxAmount / TotalAmount（税込）を分離。LastReceipt を保持し、PrintTaxInvoiceCommand（領収書ボタン）を追加。売上確定後は印字前に明細を締め、印字失敗でも取引が宙に浮かないようにした。
- ViewModels/06Uriage/PosCartLine.cs: ProductCode を追加（レシートの商品コード用）。
- Views/06Uriage/PosUriageInputView.xaml: ヘッダーに［領収書］ボタンを追加。合計パネルと会計ダイアログに税抜小計・消費税を表示。明細カードのヘッダーに出していたステータスはフッターと重複していたため件数表示のみに変更。
- Resources/UIPos.xaml: PosTotalSubText を追加。PosSecondaryButton の土台を MaterialDesignOutlinedButton から ToolCommandButton に変更。
- appsettings.json: 追加した設定項目の既定値を記載。
### 技術決定 Why
- 消費税はサーバ（CvServer/PointOfSaleService）が一切持たず `Total = 数量 × 上代` のみのため、上代を税抜とみなす外税方式をクライアント側で計算する方針をユーザーと合意した。cv10 を変更しない制約があるため、画面とレシートの釣銭はクライアント計算値（税込合計に対する釣銭）を使う。Tran01Tenuri.Total は税抜のまま（売上計上としては妥当）だが、JposPayment.ChangeAmount はサーバが税抜合計から計算するため実際とズレる。
- 桁数を EscPosBuilder に持たせ、レイアウト側は「左右振り分け」「右寄せ」「幅いっぱいの区切り線」だけを指定する構成にした。用紙幅ごとに別レイアウトを書かずに済み、幅いっぱいを使える。
- 全角文字が 2 桁を占めるため、桁揃えは文字数ではなく Shift_JIS のバイト数で計算している。
- 領収書は会計後にカートを空にしても印字できるよう、確定時の ReceiptData を LastReceipt として保持する方式にした。
- PosSecondaryButton は MaterialDesignOutlinedButton 由来だと前景色がプライマリ色固定になり、同色の ColorZone PrimaryMid 上で不可視になっていた（既存の不具合）。継承前景色を使う ToolCommandButton を土台に変更した。
### 影響範囲
- 印字処理全体とレシートデータ構造。会計時の請求額が税込に変わる（従来は税抜のまま請求していた）。cv10 側のファイルは変更していない。
### 確認
- `dotnet build cvpos10.slnx` 成功（Debug / Release とも 0 警告 / 0 エラー）。
- 使い捨ての検証プログラム（スクラッチパッド、リポジトリ未追加）で 58mm / 80mm のレシート・領収書を文字列展開し、両方とも桁いっぱいに収まり折り返しが正しいことを確認。
- 用紙幅応答のパースを 8 ケース（ユーザー提示の 58mm/80mm 応答例、先頭ゴミ、未知値、NUL 欠落、区切りなし、空、null）で確認し全件期待どおり。
- 画面起動確認：ヘッダーの［領収書］ボタン（直近売上なしのため無効表示）、合計パネルの税抜小計・消費税表示を目視確認。
- 実機（TM-m30II）での印字と用紙幅問い合わせは未実施。

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
