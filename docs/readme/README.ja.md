# MyKa's Roblox Spoofer

[English](../../README.md) | [العربية](README.ar.md) | [Português (Brasil)](README.pt-BR.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [Tiếng Việt](README.vi.md) | [ไทย](README.th.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Українська](README.uk.md) | [Polski](README.pl.md) | [বাংলা](README.bn.md)

MyKa's Roblox Spoofer は Windows 向けのターミナルアプリです。バージョン 0.3 では、個別のログインウィンドウで Roblox アカウントを追加し、確認済みのアカウント情報を保存できます。システム診断、Accounts・Diagnostics・Adapters・Identity のタブ、キーボード操作、回転する 3D ロゴも備えています。 4 で Identity タブを開き、Enter または O でシステム識別情報のツールを開きます。

0.3 では、Accounts で P を押すと保存済みブラウザのアカウントを確認し、体験ページを開きます。起動要求だけでは RobloxPlayer の実行や参加アカウントは確認できません。`identity manage`、または Adapters の S で、変更前後の記録、比較、バックアップ、既存 GUID 文字列とドライバー対応 MAC 設定の変更、DHCP 更新、DNS キャッシュの消去、範囲を限定した Roblox の整理を行えます。ファームウェア識別子の書き換え、独立した同時実行環境、公開 IP の経路設定は含みません。管理者権限での実変更は、使い捨ての Windows 環境で検証が必要です。英語の[対象範囲と復元](../system-identity.md)、[体験を開く手順](../desktop-launch.md)を参照してください。

この README は日本語版です。アプリの画面とコマンドは現在も英語です。

## 必要なもの

ビルド済みアプリには Windows x64 と [.NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/download/dotnet/10.0) が必要です。ソースコードから実行する場合は .NET SDK 10 を使います。ログインには [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) とインターネット接続も必要です。

## 初回起動

プロジェクトのルートフォルダーで PowerShell を開き、ソースコードから実行します。

~~~powershell
dotnet run --project src/MyKa.Spoofer -c Release
~~~

ローカルのビルドがすでにある場合は、次のコマンドで起動できます。付属ファイルは myka.exe と同じフォルダーに置いてください。

~~~powershell
.\.artifacts\publish\myka.exe
.\.artifacts\publish\myka.exe --help
~~~

## アカウントを追加する

1. Accounts タブで A を押します。専用の WebView2 プロファイルを使う Roblox ウィンドウが開きます。
2. パスワードや確認コードは Roblox のページにだけ入力してください。ログイン後、Verify account を押します。
3. アプリが `https://users.roblox.com/v1/users/authenticated` の応答を確認します。ユーザー名と数値 ID を確認してから Save account を押します。

アカウントは Roblox の数値 ID で区別します。同じアカウントを二重に保存することはできません。再ログインには新しいプロファイルを使うため、別のアカウントでログインしたりキャンセルしたりしても、保存済みアカウントは置き換わりません。`not checked` は、現在のセッションが有効かどうかを確認していないという意味です。

## コマンド

以下のコマンドを `.\.artifacts\publish\myka.exe` の後ろに付けます。`LOCAL_ACCOUNT_ID` は `accounts list` に表示されるローカル ID に置き換えてください。Roblox の数値 ID とは異なります。追加、再ログイン、削除には対話型ターミナルが必要です。

| コマンド | 動作 |
| --- | --- |
| `accounts list` | 保存済みアカウントとローカル ID を表示します。 |
| `accounts add` | 新しいアカウントのログイン画面を開きます。 |
| `accounts login --account LOCAL_ACCOUNT_ID` | 選択したアカウントに再ログインします。 |
| `accounts remove --account LOCAL_ACCOUNT_ID` | アカウントとローカルプロファイルの削除を確認します。 |
| `accounts cleanup` | 未完了のプロファイル削除を再試行します。 |
| `doctor` | Windows、.NET、WebView2、Roblox の標準インストール先を調べます。 |
| `identity inspect` | ネットワークアダプターと Windows が報告するアドレスを表示します。 |
| `logo` | ロゴを表示します。 |
| `--help` | コマンドのヘルプを表示します。 |

## キーボード操作

| キー | 動作 |
| --- | --- |
| `← / →, Tab / Shift+Tab, 1-4` | タブを切り替えます。 |
| `↑ / ↓, Home / End, PgUp / PgDn` | アカウントを選択するか、一覧やレポートをスクロールします。 |
| `A, S / Enter, D / Delete, C` | Accounts で順に、追加、再ログイン、削除、プロファイルの整理を行います。 |
| `R` | アカウント一覧を再読み込みするか、レポートを更新します。 |
| `F1 / ?, Esc` | キー操作ガイドを開きます。Esc でガイドを閉じます。ガイドや確認画面がなければ Esc または Q で終了します。 |
| `Space` | ロゴの回転を一時停止または再開します。 |
| `Y` | 削除を確定します。他のキーならアカウントを残します。 |
| `Ctrl+C` | 処理をキャンセルし、終了コード 130 で終了します。 |

タブ画面には最低 50 列、12 行が必要です。`--plain` は色とアニメーションのない番号付きメニューを使います。`--still` はロゴを静止させて起動します。出力のリダイレクト、`NO_COLOR`、`TERM=dumb` でもプレーンテキストになります。

## 保存するデータ

データの保存先は `%LOCALAPPDATA%\MyKa\RobloxSpoofer` です。アカウントごとの WebView2 プロファイルにブラウザーのセッションデータを保存します。`accounts.json` に入るのは ID、名前、確認日時で、パスワードや Cookie は入りません。アプリは Cookie やログインフォームの入力欄を読み取りません。

フォルダーには現在の Windows ユーザー、SYSTEM、管理者がアクセスできます。メタデータはアトミックに置き換え、`.bak` のバックアップを残します。不正なデータや未知のスキーマバージョンは変更しません。アカウントの削除はローカルプロファイルを消しますが、他のデバイスの Roblox セッションは終了しません。

## うまく動かない場合

- `WebView2 unavailable` と表示されたら、WebView2 Runtime をインストールまたは修復してください。ログイン画面は指定された Roblox の HTTPS ページだけを許可します。他のサイトへ移動する外部プロバイダーのログイン方法には対応していません。
- Accounts に `unavailable` と出たら、他のアカウント操作を閉じて R を押します。破損した `accounts.json` を空のファイルで上書きしないでください。
- 整理が終わらない場合はログインウィンドウを閉じ、`accounts cleanup` を実行します。`doctor` の確認が不完全でも、Roblox が未インストールとは限りません。Store 版や標準以外のインストール先は検出しません。

直近のコード検証では 691 件のテストが成功しています。実際の Roblox ウィンドウでのログイン、2 つのアカウントでの確認、実際のターミナルでの動作は手動検証が必要です。クライアントのファイルが見つかっても、互換性や複数クライアントの同時実行を確認したことにはなりません。

## 詳しい資料

- [アーキテクチャ（英語）](../architecture.md)
- [調査内容と制限（英語）](../research.md)
- [ターミナル画面の仕様（英語）](../terminal-ui.md)
- [ビルドとテストを含む英語版 README](../../README.md)
