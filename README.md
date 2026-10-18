# Click to Call

**für Snom- und Yealink-Tischtelefone** · *for Snom and Yealink desk phones*

Wählt Telefonnummern direkt auf deinem **Snom- oder Yealink-Tischtelefon** – per Klick auf `tel:`-Links oder per Hotkey für markierte Nummern in jedem beliebigen Programm.

*English summary below.*

> Kein offizielles Produkt der Snom Technology GmbH oder der Yealink Network Technology Co., Ltd. Snom und Yealink sind Marken ihrer jeweiligen Inhaber.

## Funktionen

- **tel:-, callto:- und sip:-Links** im Browser, in Outlook, im CRM … öffnen direkt am Telefon
- **Hotkey** (Standard: `Strg + Alt + W`): Nummer irgendwo markieren, Hotkey drücken, Enter – das Telefon wählt. Ohne Markierung öffnet sich ein leeres Wählfenster.
- **Bestätigungsfenster** vor dem Wählen – für Links und markierte Nummern getrennt abschaltbar (dann wählt das Telefon sofort)
- **Benachrichtigung mit Auflegen-Taste**
- **Rufnummern-Aufbereitung**: `+49 (0)30 / 123-456` → `030123456`, optional Amtsholung
- **Identitätsauswahl** bei mehreren Leitungen (bei Snom automatisch vom Telefon gelesen)
- **Einfache Einrichtung**: Die App sucht dein Telefon beim ersten Start automatisch im Netzwerk, ein Klick verbindet – Hersteller, HTTP/HTTPS und Login werden automatisch erkannt. Eine Checkliste führt durch die restlichen Schritte.
- **Deutsch und Englisch**
- **Update-Hinweis** bei neuen Versionen auf GitHub, Installation per Klick
- Installation **ohne Admin-Rechte**, Passwort verschlüsselt (Windows DPAPI)

Unterstützte Telefone:

- **Snom**: getestet mit D385, sollte mit allen aktuellen Snom-Tischtelefonen funktionieren (D1xx, D3xx, D7xx, D8xx).
- **Yealink** (T3x, T4x, T5x) über die „Action URI“-Schnittstelle – noch nicht auf echter Hardware getestet, Rückmeldungen willkommen. Einmalig nötig: beim ersten Anruf am Telefon „Fernsteuerung erlauben“ mit OK bestätigen bzw. in der Web-Oberfläche unter *Features → Remote Control* die IP-Adresse des PCs bei **Action URI Allow IP List** eintragen. Die App zeigt die IP-Adresse dabei an.

## Installation

1. Unter [Releases](https://github.com/julianfaatz/click-to-call/releases) die Datei `ClickToCall-x.y.z-setup.exe` herunterladen und ausführen.
   *Hinweis:* Da der Installer nicht signiert ist, zeigt Windows SmartScreen evtl. eine Warnung → „Weitere Informationen“ → „Trotzdem ausführen“.
2. Die App öffnet sich und sucht dein Telefon im Netzwerk: auf das gefundene Telefon klicken (oder die IP-Adresse eingeben und **Verbinden**). Fragt das Telefon nach einem Passwort, erscheinen die Anmeldefelder automatisch.
3. Der Einrichtungs-Checkliste auf der Startseite folgen: bei **Telefonnummern anklickbar machen** auf „Aktivieren“ klicken und in den Windows-Einstellungen bei **TEL** „Click to Call“ auswählen (Windows erlaubt Programmen nicht, das selbst zu tun). Danach einen Testanruf machen – fertig.

Beim Schließen läuft die App im Infobereich der Taskleiste weiter. Klick auf das Symbol öffnet die App, Rechtsklick das Menü (Nummer wählen, Wahlwiederholung, Auflegen, Updates …). Alle Einstellungen werden automatisch gespeichert.

## Fehlersuche

| Problem | Lösung |
|---|---|
| „Benutzername oder Passwort falsch“ | Zugangsdaten der Web-Oberfläche des Telefons verwenden (nicht die SIP-Zugangsdaten). |
| Yealink: Wählen meldet Erfolg, aber das Telefon wählt nicht | In der Web-Oberfläche des Telefons unter *Features → Remote Control* die IP-Adresse des PCs bei **Action URI Allow IP List** eintragen. |
| „Zugriff verweigert (403)“ | Im Web-Interface des Telefons prüfen, ob der Benutzer Admin-Rechte hat und HTTP-Steuerung/Action-URLs erlaubt sind. |
| Telefon nicht erreichbar | IP-Adresse prüfen (im Telefonmenü unter Informationen bzw. Status), PC und Telefon müssen sich im Netz erreichen. |
| HTTPS-Fehler | Auf der Startseite „Ändern“ → „Verbinden“ – HTTP/HTTPS wird neu erkannt, selbstsignierte Zertifikate werden akzeptiert. |
| Klick auf tel:-Link macht nichts | Wählen → Links → „Aktivieren“ und in den Windows-Einstellungen die App auswählen. |
| Hotkey geht nicht | Wählen → Hotkey: wird dort „schon verwendet“ angezeigt, eine andere Kombination wählen. |

Protokoll und Einstellungen liegen in `%APPDATA%\ClickToCall`.

### So funktioniert's

Die App ruft die HTTP-Schnittstelle des Telefons auf:

```
GET http://<telefon>/command.htm?number=030123456[&outgoing_uri=user@host]
GET http://<telefon>/command.htm?key=CANCEL        (Auflegen)
GET http://<telefon>/settings.xml                  (Identitäten lesen)
```

## Entwicklung

Voraussetzungen: [.NET 10 SDK](https://dotnet.microsoft.com/download), für den Installer [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`). Empfohlen: VS Code mit *C# Dev Kit*.

```powershell
dotnet build src/ClickToCall    # bauen
dotnet test ClickToCall.slnx    # Tests
./build.ps1                     # Release-Build + Installer + Portable-ZIP nach ./artifacts
```

In VS Code: `F5` startet die App mit Debugger, `Strg+Umschalt+B` baut.

#### Ohne Telefon testen (Mock)

Debug-Builds enthalten ein simuliertes Snom- oder Yealink-Telefon auf `127.0.0.1:18080`, das dieselbe HTTP-Schnittstelle spricht – Verbinden, Login, Netzwerksuche, Wählen, Auflegen und Leitungen laufen durch den echten Code. Ein kleines Fenster „Mock-Telefon“ zeigt, was das Telefon gerade tun würde, und kann ein ausgeschaltetes Telefon simulieren.

| Argument | Wirkung |
|---|---|
| `--mock` | Mock-Telefon ohne Login |
| `--mock-login` | Mock-Telefon mit Login `admin` / `1234` |
| `--mock-yealink` | Yealink-Mock (immer mit Login `admin` / `1234`, eigene Einstellungen in `mock-yealink`) |
| `--mock-reset` | zusätzlich: Einrichtung von vorn (Mock-Einstellungen löschen) |

In VS Code gibt es dafür fertige Startkonfigurationen („Mock-Telefon …“). Der Mock nutzt eigene Einstellungen in `%APPDATA%\ClickToCall\mock`, die echte Konfiguration bleibt unberührt. Läuft die App bereits normal, vorher beenden (es darf nur eine Instanz laufen). In Release-Builds ist der Mock nicht enthalten.

### Projektstruktur

```
src/ClickToCall/
  Core/        Logik ohne UI: Telefon-Treiber (Snom, Yealink), Nummern-Aufbereitung, Einstellungen, Registry, Hotkey, Updates
  Services/    CallController – alles, was zu einem Anruf führt
  UI/          App-Fenster, Wählfenster, Benachrichtigungen, Tray-Icon, Einstellungszeile (SettingCard), (?)-Hilfesymbol
  UI/Pages/    Seiten des App-Fensters (Start, Wählen, Allgemein)
tests/         xUnit-Tests
installer/     Inno-Setup-Skript
```

Texte (DE/EN) stehen in `Core/Loc.cs`.

### Release erstellen

Versionsnummern folgen [Semantic Versioning](https://semver.org/lang/de/): Fehlerbehebungen erhöhen die letzte Stelle (0.9.**1**), neue Funktionen die mittlere (0.**10**.0).

1. In [CHANGELOG.md](CHANGELOG.md) die Einträge unter `[Unreleased]` in einen neuen Abschnitt `## [x.y.z] - JJJJ-MM-TT` verschieben, die Links am Ende ergänzen und committen.
2. Tag setzen und pushen – der Workflow baut Installer und Release automatisch und übernimmt den Changelog-Abschnitt als Release-Text (ohne Abschnitt bricht er ab):

```powershell
git tag v0.9.0
git push origin v0.9.0
```

Installierte Apps bekommen beim nächsten Start (bzw. innerhalb von 12 Stunden) einen Update-Hinweis.

## English

**Click to Call** dials phone numbers on Snom and Yealink desk phones: click `tel:`/`callto:`/`sip:` links anywhere, or select a number in any application and press the hotkey (default `Ctrl + Alt + W`). Setup is guided: the app finds the phone on the network on first start, one click connects, and a short checklist covers the rest. It comes with number normalization, identity selection, hang-up button, update notifications and a German/English UI. Download the installer from [Releases](https://github.com/julianfaatz/click-to-call/releases); no admin rights required.

## Lizenz

[MIT](LICENSE) © Julian Faatz
