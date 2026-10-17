# Click to Call for Snom

Wählt Telefonnummern direkt auf deinem **Snom-Tischtelefon** – per Klick auf `tel:`-Links oder per Hotkey für markierte Nummern in jedem beliebigen Programm.

*English summary below.*

> Kein offizielles Produkt der Snom Technology GmbH. Snom ist eine Marke der Snom Technology GmbH.

## Funktionen

- **tel:-, callto:- und sip:-Links** im Browser, in Outlook, im CRM … öffnen direkt am Telefon
- **Hotkey** (Standard: `Strg + Alt + W`): Nummer irgendwo markieren, Hotkey drücken, Enter – das Telefon wählt. Ohne Markierung öffnet sich ein leeres Wählfenster.
- **Bestätigungsfenster** vor dem Wählen (abschaltbar)
- **Benachrichtigung mit Auflegen-Taste**
- **Rufnummern-Aufbereitung**: `+49 (0)30 / 123-456` → `030123456`, optional Amtsholung
- **Identitätsauswahl** bei mehreren Leitungen (wird vom Telefon gelesen)
- **Einrichtungsassistent** mit Verbindungstest und Testanruf
- **Deutsch und Englisch**
- **Update-Hinweis** bei neuen Versionen auf GitHub, Installation per Klick
- Installation **ohne Admin-Rechte**, Passwort verschlüsselt (Windows DPAPI)

Getestet mit Snom D385, sollte mit allen aktuellen Snom-Tischtelefonen funktionieren (D1xx, D3xx, D7xx, D8xx).

## Installation

1. Unter [Releases](https://github.com/julianfaatz/click-to-call-for-snom/releases) die Datei `ClickToCallForSnom-x.y.z-setup.exe` herunterladen und ausführen.
   *Hinweis:* Da der Installer nicht signiert ist, zeigt Windows SmartScreen evtl. eine Warnung → „Weitere Informationen“ → „Trotzdem ausführen“.
2. Der Einrichtungsassistent startet automatisch: IP-Adresse und Web-Zugangsdaten des Telefons eintragen, Verbindung testen.
3. Im Schritt **Links** auf „Windows-Standard-Apps öffnen“ klicken und bei **TEL** „Click to Call for Snom“ auswählen. (Windows erlaubt Programmen nicht, das selbst zu tun.)

Die App läuft danach im Infobereich der Taskleiste. Linksklick auf das Symbol öffnet das Wählfenster, Rechtsklick das Menü (Einstellungen, Wahlwiederholung, Auflegen, Updates …).

## Fehlersuche

| Problem | Lösung |
|---|---|
| „Benutzername oder Passwort falsch“ | Zugangsdaten der Web-Oberfläche des Telefons verwenden (nicht die SIP-Zugangsdaten). |
| „Zugriff verweigert (403)“ | Im Web-Interface des Telefons prüfen, ob der Benutzer Admin-Rechte hat und HTTP-Steuerung/Action-URLs erlaubt sind. |
| Telefon nicht erreichbar | IP-Adresse prüfen (am Telefon: Menü → Informationen), PC und Telefon müssen sich im Netz erreichen. |
| HTTPS-Fehler | Snom-Telefone nutzen selbstsignierte Zertifikate → „Zertifikat nicht prüfen“ aktivieren. |
| Klick auf tel:-Link macht nichts | Einstellungen → Links → „Windows-Standard-Apps öffnen“ und die App auswählen. |
| Hotkey geht nicht | Einstellungen → Hotkey: wird dort „bereits verwendet“ angezeigt, eine andere Kombination wählen. |

Protokoll und Einstellungen liegen in `%APPDATA%\ClickToCallForSnom`.

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
dotnet build src/ClickToCallForSnom      # bauen
dotnet test ClickToCallForSnom.slnx      # Tests
./build.ps1                              # Release-Build + Installer + Portable-ZIP nach ./artifacts
```

In VS Code: `F5` startet die App mit Debugger, `Strg+Umschalt+B` baut.

### Projektstruktur

```
src/ClickToCallForSnom/
  Core/        Logik ohne UI: Snom-Client, Nummern-Aufbereitung, Einstellungen, Registry, Hotkey, Updates
  Services/    CallController – alles, was zu einem Anruf führt
  UI/          Fenster (Wählfenster, Benachrichtigung, Einstellungen, Assistent) und Tray-Icon
  UI/Pages/    Einstellungsseiten, die Assistent und Einstellungsfenster gemeinsam nutzen
tests/         xUnit-Tests
installer/     Inno-Setup-Skript
```

Texte (DE/EN) stehen in `Core/Loc.cs`.

### Release erstellen

Version im Tag – der Workflow baut Installer und Release automatisch:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Installierte Apps bekommen beim nächsten Start (bzw. innerhalb von 12 Stunden) einen Update-Hinweis.

## English

**Click to Call for Snom** dials phone numbers on Snom desk phones: click `tel:`/`callto:`/`sip:` links anywhere, or select a number in any application and press the hotkey (default `Ctrl + Alt + W`). It comes with a setup assistant, number normalization, identity selection, hang-up button, update notifications and a German/English UI. Download the installer from [Releases](https://github.com/julianfaatz/click-to-call-for-snom/releases); no admin rights required.

## Lizenz

[MIT](LICENSE) © Julian Faatz
