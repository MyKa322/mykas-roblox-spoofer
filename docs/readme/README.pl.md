# MyKa's Roblox Spoofer

[English](../../README.md) | [العربية](README.ar.md) | [Português (Brasil)](README.pt-BR.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [Tiếng Việt](README.vi.md) | [ไทย](README.th.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Українська](README.uk.md) | [Polski](README.pl.md) | [বাংলা](README.bn.md)

MyKa's Roblox Spoofer to aplikacja terminalowa dla Windows. Wersja 0.3 pozwala dodawać konta Roblox w osobnych oknach logowania, zapisywać potwierdzone dane kont i sprawdzać system. Interfejs ma zakładki Accounts, Diagnostics, Adapters i Identity, obsługę klawiatury oraz obracające się logo 3D. Naciśnij 4, aby otworzyć Identity, a potem Enter lub O, aby przejść do narzędzi identyfikatorów systemu.

W wersji 0.3 klawisz P w Accounts sprawdza konto w zapisanym profilu przeglądarki i otwiera stronę doświadczenia. Żądanie uruchomienia nie potwierdza działania RobloxPlayer ani konta, które dołączyło. `identity manage` lub S w Adapters otwiera zapis stanu przed/po, porównanie, kopię zapasową i zmiany istniejących ciągów GUID oraz obsługiwanych przez sterownik ustawień MAC po przejrzeniu planu. Dostępne są też odnowienie DHCP, opróżnienie pamięci DNS i czyszczenie określonych danych Roblox. Nie zmienia to identyfikatorów firmware, nie tworzy niezależnych komputerów działających równocześnie ani nowej trasy z publicznym IP. Rzeczywiste zmiany wymagające administratora trzeba jeszcze sprawdzić na przeznaczonej do testów instalacji Windows. Instrukcje po angielsku: [zakres i odzyskiwanie](../system-identity.md), [otwieranie doświadczenia](../desktop-launch.md).

Ten README jest po polsku. Interfejs aplikacji i polecenia pozostają po angielsku.

## Wymagania

Gotowa aplikacja wymaga Windows x64 i [.NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/download/dotnet/10.0). Do uruchomienia kodu źródłowego potrzebny jest .NET SDK 10. Logowanie wymaga także [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) i połączenia z internetem.

## Pierwsze uruchomienie

Otwórz PowerShell w głównym katalogu projektu i uruchom aplikację z kodu źródłowego:

~~~powershell
dotnet run --project src/MyKa.Spoofer -c Release
~~~

Jeśli lokalna kompilacja jest już gotowa, uruchom ją tak. Pozostaw pliki towarzyszące w tym samym folderze co myka.exe:

~~~powershell
.\.artifacts\publish\myka.exe
.\.artifacts\publish\myka.exe --help
~~~

## Dodawanie konta

1. W zakładce Accounts naciśnij A. Otworzy się okno Roblox z osobnym profilem WebView2.
2. Wpisuj hasło i kody potwierdzające wyłącznie na stronie Roblox. Po zalogowaniu kliknij Verify account.
3. Aplikacja sprawdzi odpowiedź z `https://users.roblox.com/v1/users/authenticated`. Sprawdź nazwę oraz numeryczny identyfikator i kliknij Save account.

Konta są rozpoznawane po numerycznym Roblox ID. Nie można zapisać tego samego konta dwa razy. Ponowne logowanie używa nowego profilu; inne konto lub anulowanie nie zastępują zapisanego konta. `not checked` oznacza, że ważność sesji nie została teraz sprawdzona.

## Polecenia

Dopisz wybrane polecenie po `.\.artifacts\publish\myka.exe`. Zastąp `LOCAL_ACCOUNT_ID` lokalnym ID z `accounts list`, a nie numerycznym Roblox ID. Dodawanie, ponowne logowanie i usuwanie wymagają interaktywnego terminala.

| Polecenie | Działanie |
| --- | --- |
| `accounts list` | Wyświetla zapisane konta i lokalne ID. |
| `accounts add` | Otwiera logowanie dla nowego konta. |
| `accounts login --account LOCAL_ACCOUNT_ID` | Ponownie loguje wybrane konto. |
| `accounts remove --account LOCAL_ACCOUNT_ID` | Prosi o potwierdzenie usunięcia konta i lokalnego profilu. |
| `accounts cleanup` | Ponawia niedokończone czyszczenie profili. |
| `doctor` | Sprawdza Windows, .NET, WebView2 i domyślny folder Roblox. |
| `identity inspect` | Wyświetla karty sieciowe i adresy zgłoszone przez Windows. |
| `logo` | Wyświetla logo. |
| `--help` | Wyświetla pomoc dotyczącą poleceń. |

## Sterowanie

| Klawisze | Działanie |
| --- | --- |
| `← / →, Tab / Shift+Tab, 1-4` | Zmiana zakładki. |
| `↑ / ↓, Home / End, PgUp / PgDn` | Wybór konta lub przewijanie list i raportów. |
| `A, S / Enter, D / Delete, C` | W Accounts: dodaj, zaloguj ponownie, usuń, wyczyść profile. |
| `R` | Ponowne wczytanie kont lub odświeżenie raportu. |
| `F1 / ?, Esc` | Otwarcie pomocy klawiatury; Esc ją zamyka. Bez pomocy lub potwierdzenia Esc albo Q zamyka aplikację. |
| `Space` | Zatrzymanie lub wznowienie obrotu logo. |
| `Y` | Potwierdzenie usunięcia. Każdy inny klawisz zachowuje konto. |
| `Ctrl+C` | Anulowanie i wyjście z kodem 130. |

Zakładki wymagają okna o rozmiarze co najmniej 50 kolumn i 12 wierszy. `--plain` włącza numerowane menu bez kolorów i animacji; `--still` uruchamia aplikację z nieruchomym logo. Przekierowanie wyjścia, `NO_COLOR` i `TERM=dumb` również włączają zwykły tekst.

## Przechowywane dane

Dane znajdują się w `%LOCALAPPDATA%\MyKa\RobloxSpoofer`. Każde konto ma osobny profil WebView2 z danymi sesji przeglądarki. `accounts.json` przechowuje ID, nazwy i czas weryfikacji, bez haseł i plików cookie. Aplikacja nie odczytuje plików cookie ani pól formularza logowania.

Dostęp do folderu mają bieżący użytkownik Windows, SYSTEM i administratorzy. Metadane są zastępowane atomowo z kopią `.bak`; nieprawidłowe dane i nieznana wersja schematu pozostają bez zmian. Usunięcie konta usuwa lokalny profil, ale nie kończy sesji Roblox na innych urządzeniach.

## Rozwiązywanie problemów

- Przy komunikacie `WebView2 unavailable` zainstaluj lub napraw WebView2 Runtime. Okno logowania dopuszcza tylko przewidziane strony Roblox przez HTTPS. Logowanie przez innych dostawców, które przekierowuje na inne strony, nie jest obsługiwane.
- Jeśli Accounts pokazuje `unavailable`, zakończ inne operacje na kontach i naciśnij R. Nie zastępuj uszkodzonego `accounts.json` pustym plikiem.
- Jeśli czyszczenie pozostaje niedokończone, zamknij okna logowania i uruchom `accounts cleanup`. Niepełny wynik `doctor` nie oznacza braku Roblox. Instalacje ze Store i niestandardowe foldery nie są wykrywane.

Ostatnia weryfikacja kodu zakończyła się powodzeniem 691 testów. Logowanie w rzeczywistym oknie Roblox, sprawdzenie dwóch kont i zachowanie w prawdziwych terminalach nadal wymagają testów ręcznych. Obecność plików klienta nie potwierdza zgodności ani pracy kilku klientów naraz.

## Więcej informacji

- [Architektura (angielski)](../architecture.md)
- [Badania i ograniczenia (angielski)](../research.md)
- [Interfejs terminalowy (angielski)](../terminal-ui.md)
- [Pełny README po angielsku: kompilacja i testy](../../README.md)
