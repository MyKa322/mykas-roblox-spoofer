# MyKa's Roblox Spoofer

[English](../../README.md) | [العربية](README.ar.md) | [Português (Brasil)](README.pt-BR.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [Tiếng Việt](README.vi.md) | [ไทย](README.th.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Українська](README.uk.md) | [Polski](README.pl.md) | [বাংলা](README.bn.md)

MyKa's Roblox Spoofer 是一款 Windows 終端機應用程式。0.3 版可以透過獨立登入視窗新增 Roblox 帳號，儲存經過確認的帳號資料，並查看系統診斷資訊。介面包含 Accounts、Diagnostics、Adapters 和 Identity 四個分頁，支援鍵盤操作，並顯示旋轉的 3D 標誌。 按 4 開啟 Identity，再按 Enter 或 O 可進入識別資訊工具視窗。

0.3 版中，在 Accounts 按 P 可驗證已儲存的瀏覽器帳號並開啟體驗頁面。發出啟動請求不代表 RobloxPlayer 已執行或以正確帳號加入。執行 `identity manage`，或在 Adapters 按 S，可儲存變更前後的快照、比較數值、匯出備份，並在確認後變更既有的 GUID 字串與驅動程式支援的 MAC 設定，也可更新 DHCP 租約、清除 DNS 快取與執行限定範圍的 Roblox 清理。這些操作不改寫韌體識別碼、不建立獨立的並行電腦環境，也不設定新的公用 IP。需要系統管理員權限的實際變更仍須在可捨棄的 Windows 安裝中驗證。請參閱英文版[範圍與復原說明](../system-identity.md)及[體驗啟動說明](../desktop-launch.md)。

本 README 為繁體中文說明。應用程式介面和指令目前仍使用英文。

## 執行需求

已編譯的應用程式需要 Windows x64 和 [.NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/download/dotnet/10.0)。從原始碼執行需要 .NET SDK 10。登入帳號還需要 [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) 與網路連線。

## 第一次執行

在專案根目錄開啟 PowerShell，從原始碼執行：

~~~powershell
dotnet run --project src/MyKa.Spoofer -c Release
~~~

如果已有本機編譯版本，可以直接執行下列指令。請將隨附檔案與 myka.exe 放在同一個資料夾：

~~~powershell
.\.artifacts\publish\myka.exe
.\.artifacts\publish\myka.exe --help
~~~

## 新增帳號

1. 在 Accounts 分頁按 A，開啟使用獨立 WebView2 設定檔的 Roblox 登入視窗。
2. 只在 Roblox 頁面輸入密碼和驗證碼。登入後，按下 Verify account。
3. 應用程式會檢查 `https://users.roblox.com/v1/users/authenticated` 的回應。確認使用者名稱和數字 ID 後，按下 Save account。

應用程式以 Roblox 數字 ID 區分帳號，不允許重複儲存同一個帳號。重新登入時會建立新的瀏覽器設定檔；登入其他帳號或取消操作都不會取代已儲存的帳號。`not checked` 表示尚未檢查目前的工作階段是否仍然有效。

## 指令

將下列指令接在 `.\.artifacts\publish\myka.exe` 後面。請以 `accounts list` 顯示的本機 ID 取代 `LOCAL_ACCOUNT_ID`，不要使用 Roblox 數字 ID。新增、重新登入和移除帳號需要互動式終端機。

| 指令 | 用途 |
| --- | --- |
| `accounts list` | 列出已儲存的帳號及其本機 ID。 |
| `accounts add` | 開啟新帳號的登入視窗。 |
| `accounts login --account LOCAL_ACCOUNT_ID` | 重新登入選取的帳號。 |
| `accounts remove --account LOCAL_ACCOUNT_ID` | 確認移除帳號及其本機瀏覽器設定檔。 |
| `accounts cleanup` | 重試尚未完成的設定檔清理。 |
| `doctor` | 檢查 Windows、.NET、WebView2 和 Roblox 預設安裝目錄。 |
| `identity inspect` | 顯示網路介面卡及 Windows 回報的位址。 |
| `logo` | 顯示標誌。 |
| `--help` | 顯示指令說明。 |

## 鍵盤操作

| 按鍵 | 用途 |
| --- | --- |
| `← / →, Tab / Shift+Tab, 1-4` | 切換分頁。 |
| `↑ / ↓, Home / End, PgUp / PgDn` | 選取帳號，或捲動清單與報告。 |
| `A, S / Enter, D / Delete, C` | 在 Accounts 中依序對應：新增、重新登入、移除、清理設定檔。 |
| `R` | 重新載入帳號清單或更新報告。 |
| `F1 / ?, Esc` | 開啟快捷鍵指南；Esc 關閉指南。未開啟指南或確認提示時，Esc 或 Q 結束應用程式。 |
| `Space` | 暫停或繼續旋轉標誌。 |
| `Y` | 確認移除。其他任何按鍵都會保留帳號。 |
| `Ctrl+C` | 取消操作，並以結束代碼 130 關閉。 |

分頁介面至少需要 50 欄、12 列的視窗。`--plain` 使用無色彩、無動畫的編號選單；`--still` 啟動時讓標誌保持靜止。重新導向輸出、設定 `NO_COLOR` 或 `TERM=dumb` 時也會使用純文字。

## 資料儲存方式

資料儲存在 `%LOCALAPPDATA%\MyKa\RobloxSpoofer`。每個帳號都有獨立的 WebView2 設定檔，內含瀏覽器工作階段資料。`accounts.json` 儲存 ID、名稱和驗證時間，不儲存密碼或 Cookie。應用程式不會讀取 Cookie 或登入表單欄位。

資料夾允許目前的 Windows 使用者、SYSTEM 和系統管理員存取。中繼資料以不可分割的替換方式寫入，並保留 `.bak` 備份；無效資料或未知結構版本會維持原狀。移除帳號會刪除本機設定檔，但不會登出其他裝置上的 Roblox 工作階段。

## 遇到問題時

- 出現 `WebView2 unavailable` 時，請安裝或修復 WebView2 Runtime。登入視窗只允許預定的 Roblox HTTPS 頁面；需要導向其他網站的第三方登入方式尚未支援。
- Accounts 顯示 `unavailable` 時，先關閉其他帳號操作，再按 R 重試。請勿以空白檔案覆蓋損壞的 `accounts.json`。
- 清理未完成時，關閉登入視窗並執行 `accounts cleanup`。`doctor` 檢查不完整不代表未安裝 Roblox；它無法偵測 Store 安裝版本或非預設目錄中的安裝。

最近一次程式碼驗證有 691 項測試通過。實際 Roblox 視窗的登入流程、兩個帳號的使用情況，以及真實終端機中的表現，仍需手動檢查。找到用戶端檔案不代表已確認相容性或多用戶端執行能力。

## 進一步閱讀

- [架構說明（英文）](../architecture.md)
- [研究與限制（英文）](../research.md)
- [終端機介面說明（英文）](../terminal-ui.md)
- [完整英文 README：建置與測試](../../README.md)
