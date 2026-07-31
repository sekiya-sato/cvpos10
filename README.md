# CV POS

CV10のCvServerへgRPC接続するWPF POSクライアントです。USBキーボード入力型のバーコードスキャナで商品を読み取り、DM-D30への会計表示、会計確定、TM-m30IIのレシート印字を行います。

## 初期設定

`Dmd30CustomerDisplay/appsettings.json` に接続先と店舗情報を設定します。

- `ServerUrl`: CvServerのHTTPS URL
- `StoreId`、`WarehouseId`、`StaffId`: CV10マスタのID
- `DisplayPortName`、`DisplayBaudRate`: DM-D30のCOM設定
- `PrinterPortName`、`PrinterBaudRate`: TM-m30IIの仮想COM設定

起動時にCV10と同じ`ILoginService`でログインします。取得したJWTは`%LOCALAPPDATA%/CVPOS/clientsettings.json`へ保存され、次回起動時はトークンリフレッシュに成功した場合に再利用します。

## 利用手順

1. DM-D30とTM-m30IIを接続します。
2. バーコードを読み取ります。読取ごとにDM-D30へ明細点数・金額と合計点数・金額を送ります。
3. 会計を押し、現金・カード・その他金種を入力します。
4. `売上確定・印字`を押します。CvServerが売上・在庫集計・決済内訳を確定してから、TM-m30IIへレシートを印字します。

POSの売上確定gRPCはJWT認証必須です。カード決済端末との与信連携は含まず、カード金額を決済内訳として記録します。
