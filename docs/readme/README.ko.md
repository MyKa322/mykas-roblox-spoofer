# MyKa's Roblox Spoofer

[English](../../README.md) | [العربية](README.ar.md) | [Português (Brasil)](README.pt-BR.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [Tiếng Việt](README.vi.md) | [ไทย](README.th.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Українська](README.uk.md) | [Polski](README.pl.md) | [বাংলা](README.bn.md)

MyKa's Roblox Spoofer는 Windows용 터미널 앱입니다. 버전 0.3에서는 별도의 로그인 창으로 Roblox 계정을 추가하고, 확인된 계정 정보를 저장하며, 시스템 진단 결과를 볼 수 있습니다. Accounts, Diagnostics, Adapters, Identity 탭과 키보드 조작, 회전하는 3D 로고가 있습니다. 4로 Identity 탭을 연 뒤 Enter 또는 O를 누르면 시스템 식별 정보 도구가 열립니다.

0.3에서는 Accounts에서 P를 누르면 저장된 브라우저 계정을 확인한 뒤 체험 페이지를 엽니다. 실행 요청만으로 RobloxPlayer가 실행됐거나 올바른 계정으로 참가했는지 확인할 수는 없습니다. `identity manage` 또는 Adapters의 S로 변경 전후 기록, 비교, 백업, 기존 GUID 문자열과 드라이버가 지원하는 MAC 설정 변경, DHCP 갱신, DNS 캐시 삭제, 범위를 제한한 Roblox 정리를 사용할 수 있습니다. 펌웨어 식별자 변경, 독립된 동시 실행 환경, 새 공인 IP 경로 설정은 포함되지 않습니다. 관리자 권한으로 실제 설정을 변경하는 기능은 폐기 가능한 Windows 설치에서 검증해야 합니다. 영어 [범위 및 복원 안내](../system-identity.md)와 [체험 열기 안내](../desktop-launch.md)를 참고하세요.

이 README는 한국어 안내서입니다. 앱 화면과 명령어는 현재 영어로 표시됩니다.

## 필요한 환경

빌드된 앱을 실행하려면 Windows x64와 [.NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/download/dotnet/10.0)가 필요합니다. 소스 코드에서 실행하려면 .NET SDK 10을 사용하세요. 계정 로그인에는 [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)과 인터넷 연결도 필요합니다.

## 처음 실행하기

프로젝트 루트 폴더에서 PowerShell을 열고 소스 코드로 실행합니다.

~~~powershell
dotnet run --project src/MyKa.Spoofer -c Release
~~~

로컬 빌드가 이미 있다면 다음 명령으로 실행할 수 있습니다. 함께 제공되는 파일은 myka.exe와 같은 폴더에 두세요.

~~~powershell
.\.artifacts\publish\myka.exe
.\.artifacts\publish\myka.exe --help
~~~

## 계정 추가하기

1. Accounts 탭에서 A를 누릅니다. 독립된 WebView2 프로필을 사용하는 Roblox 창이 열립니다.
2. 비밀번호와 인증 코드는 Roblox 페이지에만 입력하세요. 로그인한 다음 Verify account를 누릅니다.
3. 앱이 `https://users.roblox.com/v1/users/authenticated`의 응답을 확인합니다. 사용자 이름과 숫자 ID를 확인한 뒤 Save account를 누릅니다.

계정은 Roblox 숫자 ID로 구분하며 같은 계정을 두 번 저장할 수 없습니다. 다시 로그인할 때는 새 프로필을 사용합니다. 다른 계정으로 로그인하거나 취소해도 저장된 계정은 바뀌지 않습니다. `not checked`는 현재 세션이 아직 유효한지 확인하지 않았다는 뜻입니다.

## 명령어

아래 명령을 `.\.artifacts\publish\myka.exe` 뒤에 붙이세요. `LOCAL_ACCOUNT_ID`는 `accounts list`에 나오는 로컬 ID로 바꿉니다. Roblox 숫자 ID와는 다릅니다. 계정 추가, 재로그인, 삭제에는 대화형 터미널이 필요합니다.

| 명령어 | 동작 |
| --- | --- |
| `accounts list` | 저장된 계정과 로컬 ID를 표시합니다. |
| `accounts add` | 새 계정의 로그인 창을 엽니다. |
| `accounts login --account LOCAL_ACCOUNT_ID` | 선택한 계정에 다시 로그인합니다. |
| `accounts remove --account LOCAL_ACCOUNT_ID` | 계정과 로컬 프로필을 삭제할지 확인합니다. |
| `accounts cleanup` | 미완료 프로필 정리를 다시 시도합니다. |
| `doctor` | Windows, .NET, WebView2와 Roblox 기본 설치 폴더를 확인합니다. |
| `identity inspect` | 네트워크 어댑터와 Windows가 보고한 주소를 표시합니다. |
| `logo` | 로고를 표시합니다. |
| `--help` | 명령어 도움말을 표시합니다. |

## 키보드 조작

| 키 | 동작 |
| --- | --- |
| `← / →, Tab / Shift+Tab, 1-4` | 탭을 전환합니다. |
| `↑ / ↓, Home / End, PgUp / PgDn` | 계정을 선택하거나 목록과 보고서를 스크롤합니다. |
| `A, S / Enter, D / Delete, C` | Accounts에서 차례로 추가, 재로그인, 삭제, 프로필 정리를 수행합니다. |
| `R` | 계정 목록을 다시 읽거나 보고서를 새로 고칩니다. |
| `F1 / ?, Esc` | 단축키 안내를 엽니다. Esc는 안내를 닫습니다. 안내나 확인 창이 없으면 Esc 또는 Q로 앱을 종료합니다. |
| `Space` | 로고 회전을 멈추거나 다시 시작합니다. |
| `Y` | 삭제를 확정합니다. 다른 키를 누르면 계정을 유지합니다. |
| `Ctrl+C` | 취소하고 종료 코드 130으로 종료합니다. |

탭 화면에는 최소 50열, 12행이 필요합니다. `--plain`은 색과 애니메이션 없이 번호 메뉴를 표시하고, `--still`은 로고를 멈춘 상태로 시작합니다. 출력 리디렉션, `NO_COLOR`, `TERM=dumb`도 일반 텍스트를 사용합니다.

## 데이터 저장 방식

데이터는 `%LOCALAPPDATA%\MyKa\RobloxSpoofer`에 저장됩니다. 계정별 WebView2 프로필에는 브라우저 세션 데이터가 들어 있습니다. `accounts.json`은 ID, 이름, 확인 시각을 저장하며 비밀번호나 쿠키는 저장하지 않습니다. 앱은 쿠키나 로그인 양식의 입력 필드를 읽지 않습니다.

폴더 접근 권한은 현재 Windows 사용자, SYSTEM, 관리자에게 있습니다. 메타데이터는 원자적으로 교체되고 `.bak` 백업을 남깁니다. 잘못된 데이터나 알 수 없는 스키마 버전은 변경하지 않습니다. 계정 삭제는 로컬 프로필을 지우지만 다른 기기의 Roblox 세션을 종료하지는 않습니다.

## 문제가 생겼을 때

- `WebView2 unavailable`이 표시되면 WebView2 Runtime을 설치하거나 복구하세요. 로그인 창은 지정된 Roblox HTTPS 페이지만 허용합니다. 다른 사이트로 이동하는 외부 제공업체 로그인 방식은 지원하지 않습니다.
- Accounts가 `unavailable`을 표시하면 다른 계정 작업을 닫고 R을 누르세요. 손상된 `accounts.json`을 빈 파일로 덮어쓰지 마세요.
- 정리가 끝나지 않으면 로그인 창을 닫고 `accounts cleanup`을 실행하세요. `doctor` 확인이 불완전하다고 해서 Roblox가 설치되지 않았다는 뜻은 아닙니다. Store 설치본이나 기본 경로가 아닌 설치는 감지하지 않습니다.

최근 코드 검증에서 691개 테스트가 통과했습니다. 실제 Roblox 창에서의 로그인, 두 계정 사용, 실제 터미널 동작은 아직 수동 확인이 필요합니다. 클라이언트 파일이 있다는 사실만으로 호환성이나 여러 클라이언트의 동시 실행을 확인할 수는 없습니다.

## 더 알아보기

- [아키텍처 (영어)](../architecture.md)
- [조사 내용과 제한 사항 (영어)](../research.md)
- [터미널 인터페이스 (영어)](../terminal-ui.md)
- [빌드와 테스트 안내가 있는 전체 영어 README](../../README.md)
