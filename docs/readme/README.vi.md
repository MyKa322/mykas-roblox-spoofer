# MyKa's Roblox Spoofer

[English](../../README.md) | [العربية](README.ar.md) | [Português (Brasil)](README.pt-BR.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [Tiếng Việt](README.vi.md) | [ไทย](README.th.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Українська](README.uk.md) | [Polski](README.pl.md) | [বাংলা](README.bn.md)

MyKa's Roblox Spoofer là ứng dụng chạy trong terminal trên Windows. Phiên bản 0.3 cho phép thêm tài khoản Roblox qua cửa sổ đăng nhập riêng, lưu thông tin tài khoản đã xác nhận và xem kết quả chẩn đoán hệ thống. Giao diện có các tab Accounts, Diagnostics, Adapters và Identity, điều khiển bằng bàn phím và logo 3D xoay. Nhấn 4 để mở tab Identity, rồi nhấn Enter hoặc O để mở các công cụ định danh hệ thống.

Ở bản 0.3, P trong Accounts kiểm tra tài khoản của hồ sơ trình duyệt đã lưu rồi mở trang trải nghiệm. Gửi yêu cầu khởi chạy chưa xác nhận RobloxPlayer đã chạy hoặc tham gia bằng đúng tài khoản. `identity manage`, hoặc S trong Adapters, mở chức năng chụp trước/sau, so sánh, sao lưu và thay đổi có bước xem lại đối với chuỗi GUID hiện có cùng thiết lập MAC được trình điều khiển hỗ trợ. Ngoài ra còn có gia hạn DHCP, xóa bộ nhớ đệm DNS và dọn dẹp Roblox trong phạm vi giới hạn. Những thao tác này không sửa mã định danh phần sụn, tạo các máy độc lập chạy đồng thời hay cấu hình IP công cộng mới. Thay đổi thực tế với quyền quản trị cần được thử trên bản cài Windows có thể bỏ đi. Hướng dẫn tiếng Anh: [phạm vi và khôi phục](../system-identity.md), [mở trải nghiệm](../desktop-launch.md).

README này được viết bằng tiếng Việt. Giao diện ứng dụng và các lệnh hiện vẫn dùng tiếng Anh.

## Yêu cầu

Bản đã biên dịch cần Windows x64 và [.NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/download/dotnet/10.0). Muốn chạy từ mã nguồn, bạn cần .NET SDK 10. Đăng nhập tài khoản còn cần [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) và kết nối internet.

## Chạy lần đầu

Mở PowerShell tại thư mục gốc của dự án rồi chạy ứng dụng từ mã nguồn:

~~~powershell
dotnet run --project src/MyKa.Spoofer -c Release
~~~

Nếu đã có bản biên dịch cục bộ, bạn có thể chạy bằng các lệnh sau. Giữ các tệp đi kèm trong cùng thư mục với myka.exe:

~~~powershell
.\.artifacts\publish\myka.exe
.\.artifacts\publish\myka.exe --help
~~~

## Thêm tài khoản

1. Trong tab Accounts, nhấn A. Một cửa sổ Roblox sẽ mở với hồ sơ WebView2 riêng.
2. Chỉ nhập mật khẩu và mã xác minh trên trang Roblox. Sau khi đăng nhập, chọn Verify account.
3. Ứng dụng kiểm tra phản hồi từ `https://users.roblox.com/v1/users/authenticated`. Kiểm tra tên và ID dạng số, rồi chọn Save account.

Ứng dụng phân biệt tài khoản bằng ID dạng số của Roblox và không cho lưu trùng một tài khoản. Khi đăng nhập lại, ứng dụng dùng hồ sơ mới. Đăng nhập nhầm tài khoản hoặc hủy thao tác sẽ không thay thế tài khoản đã lưu. `not checked` nghĩa là ứng dụng chưa kiểm tra phiên đăng nhập hiện còn hiệu lực hay không.

## Các lệnh

Thêm lệnh bên dưới sau `.\.artifacts\publish\myka.exe`. Thay `LOCAL_ACCOUNT_ID` bằng ID cục bộ từ `accounts list`, không dùng ID dạng số của Roblox. Thêm tài khoản, đăng nhập lại và xóa tài khoản cần terminal tương tác.

| Lệnh | Chức năng |
| --- | --- |
| `accounts list` | Liệt kê tài khoản đã lưu và ID cục bộ. |
| `accounts add` | Mở cửa sổ đăng nhập cho tài khoản mới. |
| `accounts login --account LOCAL_ACCOUNT_ID` | Đăng nhập lại tài khoản đã chọn. |
| `accounts remove --account LOCAL_ACCOUNT_ID` | Yêu cầu xác nhận xóa tài khoản và hồ sơ cục bộ. |
| `accounts cleanup` | Thử lại việc dọn dẹp hồ sơ chưa hoàn tất. |
| `doctor` | Kiểm tra Windows, .NET, WebView2 và thư mục cài đặt Roblox mặc định. |
| `identity inspect` | Hiển thị bộ điều hợp mạng và địa chỉ do Windows báo cáo. |
| `logo` | Hiển thị logo. |
| `--help` | Hiển thị hướng dẫn lệnh. |

## Điều khiển bằng bàn phím

| Phím | Thao tác |
| --- | --- |
| `← / →, Tab / Shift+Tab, 1-4` | Chuyển tab. |
| `↑ / ↓, Home / End, PgUp / PgDn` | Chọn tài khoản hoặc cuộn danh sách và báo cáo. |
| `A, S / Enter, D / Delete, C` | Trong Accounts: thêm, đăng nhập lại, xóa, dọn dẹp hồ sơ. |
| `R` | Tải lại danh sách tài khoản hoặc làm mới báo cáo. |
| `F1 / ?, Esc` | Mở hướng dẫn phím; Esc đóng hướng dẫn. Nếu không có hướng dẫn hay hộp xác nhận, Esc hoặc Q thoát ứng dụng. |
| `Space` | Tạm dừng hoặc tiếp tục xoay logo. |
| `Y` | Xác nhận xóa. Mọi phím khác đều giữ lại tài khoản. |
| `Ctrl+C` | Hủy và thoát với mã 130. |

Giao diện tab cần cửa sổ ít nhất 50 cột và 12 dòng. `--plain` dùng menu đánh số không có màu hay chuyển động; `--still` khởi động với logo đứng yên. Chuyển hướng đầu ra, `NO_COLOR` và `TERM=dumb` cũng dùng văn bản thuần.

## Dữ liệu được lưu ở đâu

Dữ liệu nằm tại `%LOCALAPPDATA%\MyKa\RobloxSpoofer`. Mỗi tài khoản có hồ sơ WebView2 riêng chứa dữ liệu phiên trình duyệt. `accounts.json` lưu ID, tên và thời gian xác minh, không chứa mật khẩu hay cookie. Ứng dụng không đọc cookie hoặc các ô nhập liệu của biểu mẫu đăng nhập.

Thư mục cho phép người dùng Windows hiện tại, SYSTEM và quản trị viên truy cập. Siêu dữ liệu được thay thế theo thao tác nguyên tử và có bản sao `.bak`; dữ liệu không hợp lệ hoặc phiên bản lược đồ chưa biết được giữ nguyên. Xóa tài khoản sẽ xóa hồ sơ cục bộ, nhưng không kết thúc các phiên Roblox trên thiết bị khác.

## Khi gặp sự cố

- Nếu thấy `WebView2 unavailable`, hãy cài đặt hoặc sửa chữa WebView2 Runtime. Cửa sổ đăng nhập chỉ cho phép các trang Roblox HTTPS đã quy định. Cách đăng nhập qua nhà cung cấp khác có chuyển sang website bên ngoài chưa được hỗ trợ.
- Nếu Accounts hiển thị `unavailable`, đóng các thao tác tài khoản khác rồi nhấn R. Không ghi đè `accounts.json` bị hỏng bằng một tệp trống.
- Nếu dọn dẹp chưa hoàn tất, đóng cửa sổ đăng nhập rồi chạy `accounts cleanup`. Kiểm tra `doctor` không đầy đủ không có nghĩa là máy chưa cài Roblox. Công cụ không phát hiện bản cài từ Store hoặc thư mục khác mặc định.

Trong lần kiểm tra mã gần nhất, 691 bài kiểm thử đã đạt. Việc đăng nhập qua cửa sổ Roblox thật, thử hai tài khoản và kiểm tra trên terminal thực tế vẫn cần thực hiện thủ công. Có tệp trình khách không đồng nghĩa với việc đã xác nhận tính tương thích hay khả năng chạy nhiều trình khách.

## Tài liệu khác

- [Kiến trúc (tiếng Anh)](../architecture.md)
- [Nghiên cứu và giới hạn (tiếng Anh)](../research.md)
- [Giao diện terminal (tiếng Anh)](../terminal-ui.md)
- [README tiếng Anh đầy đủ, gồm cách biên dịch và kiểm thử](../../README.md)
