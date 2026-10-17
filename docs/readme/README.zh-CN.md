# MyKa's Roblox Spoofer

[English](../../README.md) | [العربية](README.ar.md) | [Português (Brasil)](README.pt-BR.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [Tiếng Việt](README.vi.md) | [ไทย](README.th.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Українська](README.uk.md) | [Polski](README.pl.md) | [বাংলা](README.bn.md)

MyKa's Roblox Spoofer 是一款 Windows 终端应用。0.3 版可以通过独立登录窗口添加 Roblox 账号，保存经过确认的账号资料，并查看系统诊断信息。界面包含 Accounts、Diagnostics、Adapters 和 Identity 四个标签页，支持键盘操作，并显示旋转的 3D 标志。 按 4 打开 Identity，再按 Enter 或 O 可进入身份工具窗口。

0.3 版中，在 Accounts 按 P 可验证已保存的浏览器账号并打开体验页面。发出启动请求不代表 RobloxPlayer 已运行或以正确账号加入。运行 `identity manage`，或在 Adapters 按 S，可保存更改前后的快照、比较数值、导出备份，并在确认后更改已有的 GUID 字符串和驱动支持的 MAC 设置，还可续租 DHCP、清除 DNS 缓存和执行限定范围的 Roblox 清理。这些操作不改写固件标识，不创建独立的并发计算机环境，也不配置新的公网 IP。需要管理员权限的实际修改仍须在可丢弃的 Windows 安装中验证。参阅英文版[范围与恢复说明](../system-identity.md)和[体验启动说明](../desktop-launch.md)。

本 README 为简体中文说明。应用界面和命令目前仍使用英文。

## 运行要求

已编译的应用需要 Windows x64 和 [.NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/download/dotnet/10.0)。从源码运行需要 .NET SDK 10。登录账号还需要 [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) 和网络连接。

## 首次运行

在项目根目录打开 PowerShell，从源码运行：

~~~powershell
dotnet run --project src/MyKa.Spoofer -c Release
~~~

如果已有本地编译版本，可以直接运行以下命令。请将配套文件与 myka.exe 保存在同一目录：

~~~powershell
.\.artifacts\publish\myka.exe
.\.artifacts\publish\myka.exe --help
~~~

## 添加账号

1. 在 Accounts 标签页按 A，打开带有独立 WebView2 配置文件的 Roblox 登录窗口。
2. 只在 Roblox 页面中输入密码和验证码。登录后，点击 Verify account。
3. 应用会检查 `https://users.roblox.com/v1/users/authenticated` 的响应。核对用户名和数字 ID 后，点击 Save account。

应用使用 Roblox 数字 ID 区分账号，不允许重复保存同一个账号。重新登录时会创建新的浏览器配置文件；登录到其他账号或取消操作都不会替换已保存的账号。`not checked` 表示应用尚未检查当前会话是否仍然有效。

## 命令

将下列命令接在 `.\.artifacts\publish\myka.exe` 后面。把 `LOCAL_ACCOUNT_ID` 替换为 `accounts list` 显示的本地 ID，而不是 Roblox 数字 ID。添加、重新登录和删除账号需要交互式终端。

| 命令 | 作用 |
| --- | --- |
| `accounts list` | 列出已保存的账号及其本地 ID。 |
| `accounts add` | 打开新账号的登录窗口。 |
| `accounts login --account LOCAL_ACCOUNT_ID` | 重新登录所选账号。 |
| `accounts remove --account LOCAL_ACCOUNT_ID` | 确认删除账号及其本地浏览器配置文件。 |
| `accounts cleanup` | 重试尚未完成的配置文件清理。 |
| `doctor` | 检查 Windows、.NET、WebView2 和 Roblox 默认安装目录。 |
| `identity inspect` | 显示网络适配器及 Windows 报告的地址。 |
| `logo` | 显示标志。 |
| `--help` | 显示命令帮助。 |

## 键盘操作

| 按键 | 作用 |
| --- | --- |
| `← / →, Tab / Shift+Tab, 1-4` | 切换标签页。 |
| `↑ / ↓, Home / End, PgUp / PgDn` | 选择账号，或滚动列表与报告。 |
| `A, S / Enter, D / Delete, C` | 在 Accounts 中依次对应：添加、重新登录、删除、清理配置文件。 |
| `R` | 重新读取账号列表或刷新报告。 |
| `F1 / ?, Esc` | 打开快捷键指南；Esc 关闭指南。未打开指南或确认提示时，Esc 或 Q 退出应用。 |
| `Space` | 暂停或继续旋转标志。 |
| `Y` | 确认删除。其他任意按键都会保留账号。 |
| `Ctrl+C` | 取消操作并以退出码 130 结束。 |

标签页界面至少需要 50 列、12 行的窗口。`--plain` 使用无颜色、无动画的编号菜单；`--still` 启动时让标志保持静止。重定向输出、设置 `NO_COLOR` 或 `TERM=dumb` 时也会使用纯文本。

## 数据保存方式

数据保存在 `%LOCALAPPDATA%\MyKa\RobloxSpoofer`。每个账号使用独立的 WebView2 配置文件，其中包含浏览器会话数据。`accounts.json` 保存 ID、名称和验证时间，不保存密码或 Cookie。应用不会读取 Cookie 或登录表单字段。

目录允许当前 Windows 用户、SYSTEM 和管理员访问。元数据通过原子替换写入，并保留 `.bak` 备份；无效数据或未知格式版本会保持原样。删除账号会移除本地配置文件，但不会注销其他设备上的 Roblox 会话。

## 遇到问题时

- 出现 `WebView2 unavailable` 时，请安装或修复 WebView2 Runtime。登录窗口只允许访问预定的 Roblox HTTPS 页面；需要跳转到其他网站的第三方登录方式暂不支持。
- Accounts 显示 `unavailable` 时，先关闭其他账号操作，再按 R 重试。不要用空文件覆盖损坏的 `accounts.json`。
- 清理未完成时，关闭登录窗口并运行 `accounts cleanup`。`doctor` 检查不完整并不代表未安装 Roblox；它无法发现 Store 安装版本或非默认目录中的安装。

最近一次代码验证有 691 项测试通过。真实 Roblox 窗口的登录流程、两个账号的使用情况以及实际终端中的表现仍需手动检查。找到客户端文件不代表已确认兼容性或多客户端运行能力。

## 进一步阅读

- [架构说明（英文）](../architecture.md)
- [研究与限制（英文）](../research.md)
- [终端界面说明（英文）](../terminal-ui.md)
- [完整英文 README：构建与测试](../../README.md)
