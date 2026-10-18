# Mitmachen

Fehlerberichte, Ideen und Pull Requests sind willkommen. Für Fehler und Wünsche gibt es [Vorlagen](https://github.com/julianfaatz/click-to-call/issues/new/choose) – Telefonmodell, Firmware und ein Ausschnitt aus dem Protokoll helfen am meisten. Größere Änderungen am besten vorher kurz in einem Issue besprechen.

## Bauen und testen

Benötigt das [.NET 10 SDK](https://dotnet.microsoft.com/download), für den Installer [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`). Empfohlen: VS Code mit *C# Dev Kit* (`F5` startet mit Debugger, `Strg+Umschalt+B` baut).

```powershell
dotnet build src/ClickToCall    # bauen
dotnet test ClickToCall.slnx    # Tests
./build.ps1                     # Installer und Portable-ZIP nach ./artifacts
```

## Ohne Telefon testen

Debug-Builds enthalten ein simuliertes Telefon auf `127.0.0.1:18080`, das dieselbe HTTP-Schnittstelle spricht. Verbinden, Login, Netzwerksuche, Wählen, Auflegen und Leitungen laufen durch den echten Code. Ein kleines Fenster „Mock-Telefon“ zeigt, was das Telefon gerade tun würde.

```powershell
dotnet run --project src/ClickToCall -- --mock
```

| Argument | Wirkung |
|---|---|
| `--mock` | Snom-Mock ohne Login |
| `--mock-login` | Snom-Mock mit Login `admin` / `1234` |
| `--mock-yealink` | Yealink-Mock mit Login `admin` / `1234` |
| `--mock-reset` | zusätzlich: Einrichtung von vorn |

In VS Code gibt es dafür Startkonfigurationen („Mock-Telefon …“). Der Mock nutzt eigene Einstellungen in `%APPDATA%\ClickToCall\mock`. Eine normal laufende Instanz vorher beenden – es darf nur eine laufen.

[tools/demo-contacts.html](tools/demo-contacts.html) ist eine Beispielseite mit Telefon-Links für Screenshots und Tests.

## Aufbau

```
src/ClickToCall/
  Core/        Logik ohne UI: Telefon-Treiber, Nummern-Aufbereitung, Einstellungen, Registry, Hotkey, Updates
  Services/    CallController – alles, was zu einem Anruf führt
  UI/          Fenster, Benachrichtigungen, Tray-Icon
  UI/Pages/    Seiten des App-Fensters (Start, Wählen, Allgemein)
tests/         xUnit-Tests
installer/     Inno-Setup-Skript
```

Texte (Deutsch/Englisch) stehen in `Core/Loc.cs`.

Die App steuert die Telefone über deren HTTP-Schnittstelle:

```
Snom     GET /command.htm?number=030123456[&outgoing_uri=user@host]   Wählen
         GET /command.htm?key=CANCEL                                  Auflegen
         GET /settings.xml                                            Identitäten lesen
Yealink  GET /servlet?key=number=030123456[&outgoing_uri=user@host]   Wählen
         GET /servlet?key=CALLEND                                     Auflegen
```

Ein weiterer Hersteller braucht einen eigenen Treiber in `Core/` (siehe `PhoneDrivers.cs`).

## Release

Versionsnummern folgen [Semantic Versioning](https://semver.org/lang/de/): Fehlerbehebungen erhöhen die letzte Stelle (0.9.**1**), neue Funktionen die mittlere (0.**10**.0).

1. In [CHANGELOG.md](CHANGELOG.md) die Einträge unter `[Unreleased]` in einen Abschnitt `## [x.y.z] - JJJJ-MM-TT` verschieben, die Links am Ende ergänzen und committen.
2. Tag setzen und pushen:

   ```powershell
   git tag v0.9.1
   git push origin v0.9.1
   ```

GitHub Actions baut dann Installer, Portable-ZIP und Prüfsummen, legt das Release mit dem Changelog-Abschnitt als Text an und reicht die Version bei winget ein (sobald eingerichtet). Installierte Apps melden das Update beim nächsten Start bzw. innerhalb von 12 Stunden.
