# MyKa's Roblox Spoofer

[English](../../README.md) | [العربية](README.ar.md) | [Português (Brasil)](README.pt-BR.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [Tiếng Việt](README.vi.md) | [ไทย](README.th.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Українська](README.uk.md) | [Polski](README.pl.md) | [বাংলা](README.bn.md)

MyKa's Roblox Spoofer ist eine Terminalanwendung für Windows. Version 0.3 verwaltet Roblox-Konten mit getrennten Anmeldefenstern und lokal gespeicherten, bestätigten Kontodaten. Dazu kommen Systemdiagnosen, die Tabs Accounts, Diagnostics, Adapters und Identity sowie ein drehendes 3D-Logo. Mit 4 öffnen Sie den Tab Identity; Enter oder O öffnet die Werkzeuge für Systemkennungen.

In Version 0.3 prüft P in Accounts das gespeicherte Browserkonto und öffnet die Erlebnis-Seite. Die Startanfrage bestätigt weder einen laufenden RobloxPlayer noch das beigetretene Konto. `identity manage` oder S in Adapters öffnet Vorher-/Nachher-Aufnahmen, Vergleich und Sicherung sowie prüfbare Änderungen vorhandener GUID-Zeichenfolgen und vom Treiber unterstützter MAC-Einstellungen. Dazu kommen DHCP-Erneuerung, DNS-Cache-Leerung und begrenzte Roblox-Bereinigung. Firmwarekennungen, unabhängig gleichzeitig laufende Rechner und eine neue öffentliche IP werden damit nicht bereitgestellt. Echte Änderungen mit Administratorrechten müssen noch auf einer entbehrlichen Windows-Installation geprüft werden. Englische Anleitungen: [Umfang und Wiederherstellung](../system-identity.md), [Erlebnis öffnen](../desktop-launch.md).

Diese Anleitung ist auf Deutsch. Die Oberfläche und die Befehle der Anwendung bleiben vorerst auf Englisch.

## Voraussetzungen

Für die fertige Anwendung brauchen Sie Windows x64 und [.NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/download/dotnet/10.0). Zum Ausführen des Quellcodes ist das .NET SDK 10 erforderlich. Für die Anmeldung werden außerdem [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) und eine Internetverbindung benötigt.

## Erster Start

Öffnen Sie PowerShell im Stammverzeichnis des Projekts und starten Sie die Anwendung aus dem Quellcode:

~~~powershell
dotnet run --project src/MyKa.Spoofer -c Release
~~~

Falls der lokale Build bereits vorliegt, können Sie ihn so starten. Lassen Sie die zugehörigen Dateien im selben Ordner wie myka.exe:

~~~powershell
.\.artifacts\publish\myka.exe
.\.artifacts\publish\myka.exe --help
~~~

## Ein Konto hinzufügen

1. Drücken Sie im Tab Accounts die Taste A. Roblox öffnet sich in einem Fenster mit eigenem WebView2-Profil.
2. Geben Sie Passwort und Bestätigungscodes nur auf der Roblox-Seite ein. Klicken Sie nach der Anmeldung auf Verify account.
3. Die Anwendung prüft die Antwort von `https://users.roblox.com/v1/users/authenticated`. Kontrollieren Sie Namen und numerische ID und klicken Sie auf Save account.

Konten werden anhand ihrer numerischen Roblox-ID unterschieden. Dasselbe Konto lässt sich nicht doppelt speichern. Eine erneute Anmeldung verwendet ein neues Profil; ein anderes Konto oder ein Abbruch ersetzt das gespeicherte Konto nicht. `not checked` bedeutet, dass die Gültigkeit der Sitzung gerade nicht geprüft wurde.

## Befehle

Hängen Sie diese Befehle an `.\.artifacts\publish\myka.exe` an. Ersetzen Sie `LOCAL_ACCOUNT_ID` durch die lokale ID aus `accounts list`, nicht durch die numerische Roblox-ID. Hinzufügen, erneute Anmeldung und Entfernen benötigen ein interaktives Terminal.

| Befehl | Funktion |
| --- | --- |
| `accounts list` | Gespeicherte Konten und lokale IDs anzeigen. |
| `accounts add` | Die Anmeldung für ein neues Konto öffnen. |
| `accounts login --account LOCAL_ACCOUNT_ID` | Für das ausgewählte Konto erneut anmelden. |
| `accounts remove --account LOCAL_ACCOUNT_ID` | Das Entfernen des Kontos und seines lokalen Profils bestätigen. |
| `accounts cleanup` | Ausstehende Profilbereinigung erneut versuchen. |
| `doctor` | Windows, .NET, WebView2 und den üblichen Roblox-Installationsordner prüfen. |
| `identity inspect` | Netzwerkadapter und die von Windows gemeldeten Adressen anzeigen. |
| `logo` | Das Logo anzeigen. |
| `--help` | Die Befehlshilfe anzeigen. |

## Tastaturbedienung

| Tasten | Funktion |
| --- | --- |
| `← / →, Tab / Shift+Tab, 1-4` | Tabs wechseln. |
| `↑ / ↓, Home / End, PgUp / PgDn` | Ein Konto auswählen oder Listen und Berichte durchblättern. |
| `A, S / Enter, D / Delete, C` | In Accounts: hinzufügen, erneut anmelden, entfernen, Profile bereinigen. |
| `R` | Kontenliste neu laden oder Bericht aktualisieren. |
| `F1 / ?, Esc` | Tastenhilfe öffnen; Esc schließt sie. Ohne Hilfe oder Rückfrage beendet Esc oder Q die Anwendung. |
| `Space` | Die Drehung des Logos anhalten oder fortsetzen. |
| `Y` | Entfernen bestätigen. Jede andere Taste behält das Konto. |
| `Ctrl+C` | Abbrechen und mit Exitcode 130 beenden. |

Die Tabs benötigen mindestens 50 Spalten und 12 Zeilen. `--plain` zeigt ein nummeriertes Menü ohne Farbe und Animation; `--still` startet mit ruhendem Logo. Umgeleitete Ausgabe, `NO_COLOR` und `TERM=dumb` verwenden ebenfalls einfachen Text.

## Gespeicherte Daten

Die Daten liegen unter `%LOCALAPPDATA%\MyKa\RobloxSpoofer`. Jedes Konto hat ein eigenes WebView2-Profil mit Browsersitzungsdaten. `accounts.json` enthält IDs, Namen und Prüfzeitpunkte, keine Passwörter oder Cookies. Die Anwendung liest keine Cookies oder Felder des Anmeldeformulars.

Auf den Ordner dürfen der aktuelle Windows-Benutzer, SYSTEM und Administratoren zugreifen. Metadaten werden atomar ersetzt und als `.bak` gesichert. Ungültige Daten und unbekannte Schemaversionen bleiben unverändert. Das Entfernen löscht das lokale Profil, meldet aber keine Roblox-Sitzungen auf anderen Geräten ab.

## Wenn etwas nicht funktioniert

- Bei `WebView2 unavailable` installieren oder reparieren Sie WebView2 Runtime. Das Anmeldefenster erlaubt nur die vorgesehenen Roblox-Seiten über HTTPS; Anmeldungen über andere Anbieter mit Weiterleitung auf fremde Websites werden nicht unterstützt.
- Zeigt Accounts `unavailable`, schließen Sie andere Kontooperationen und drücken Sie R. Ersetzen Sie eine beschädigte `accounts.json` nicht durch eine leere Datei.
- Bleibt eine Bereinigung offen, schließen Sie Anmeldefenster und führen Sie `accounts cleanup` aus. Eine unvollständige `doctor`-Prüfung belegt nicht, dass Roblox fehlt. Store-Installationen und abweichende Installationsordner werden nicht erkannt.

Bei der letzten Codeprüfung bestanden 691 Tests. Die Anmeldung im echten Roblox-Fenster, Tests mit zwei Konten und das Verhalten in echten Terminals müssen noch manuell geprüft werden. Vorhandene Clientdateien bestätigen weder Kompatibilität noch den Betrieb mehrerer Clients.

## Weitere Dokumentation

- [Architektur (Englisch)](../architecture.md)
- [Recherche und Einschränkungen (Englisch)](../research.md)
- [Terminaloberfläche (Englisch)](../terminal-ui.md)
- [Ausführliche englische README mit Build- und Testanleitung](../../README.md)
