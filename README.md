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
- **Einfache Einrichtung**: Die App sucht dein Telefon beim ersten Start automatisch im Netzwerk, ein Klick verbindet – HTTP/HTTPS und Login werden automatisch erkannt. Eine Checkliste führt durch die restlichen Schritte.
- **Deutsch und Englisch**
- **Update-Hinweis** bei neuen Versionen auf GitHub, Installation per Klick
- Installation **ohne Admin-Rechte**, Passwort verschlüsselt (Windows DPAPI)

Getestet mit Snom D385, sollte mit allen aktuellen Snom-Tischtelefonen funktionieren (D1xx, D3xx, D7xx, D8xx).

## Installation

1. Unter [Releases](https://github.com/julianfaatz/click-to-call-for-snom/releases) die Datei `ClickToCallForSnom-x.y.z-setup.exe` herunterladen und ausführen.
   *Hinweis:* Da der Installer nicht signiert ist, zeigt Windows SmartScreen evtl. eine Warnung → „Weitere Informationen“ → „Trotzdem ausführen“.
2. Die App öffnet sich und sucht dein Telefon im Netzwerk: auf das gefundene Telefon klicken (oder die IP-Adresse eingeben und **Verbinden**). Fragt das Telefon nach einem Passwort, erscheinen die Anmeldefelder automatisch.
3. Der Einrichtungs-Checkliste auf der Startseite folgen: bei **Telefonnummern anklickbar machen** auf „Aktivieren“ klicken und in den Windows-Einstellungen bei **TEL** „Click to Call for Snom“ auswählen (Windows erlaubt Programmen nicht, das selbst zu tun). Danach einen Testanruf machen – fertig.

Beim Schließen läuft die App im Infobereich der Taskleiste weiter. Klick auf das Symbol öffnet die App, Rechtsklick das Menü (Nummer wählen, Wahlwiederholung, Auflegen, Updates …). Alle Einstellungen werden automatisch gespeichert.

## Fehlersuche

| Problem | Lösung |
|---|---|
| „Benutzername oder Passwort falsch“ | Zugangsdaten der Web-Oberfläche des Telefons verwenden (nicht die SIP-Zugangsdaten). |
| „Zugriff verweigert (403)“ | Im Web-Interface des Telefons prüfen, ob der Benutzer Admin-Rechte hat und HTTP-Steuerung/Action-URLs erlaubt sind. |
| Telefon nicht erreichbar | IP-Adresse prüfen (am Telefon: Menü → Informationen), PC und Telefon müssen sich im Netz erreichen. |
| HTTPS-Fehler | Auf der Startseite „Ändern“ → „Verbinden“ – HTTP/HTTPS wird neu erkannt, selbstsignierte Zertifikate werden akzeptiert. |
| Klick auf tel:-Link macht nichts | Wählen → Links → „Aktivieren“ und in den Windows-Einstellungen die App auswählen. |
| Hotkey geht nicht | Wählen → Hotkey: wird dort „schon verwendet“ angezeigt, eine andere Kombination wählen. |

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
  UI/          App-Fenster, Wählfenster, Benachrichtigungen, Tray-Icon, Einstellungszeile (SettingCard), (?)-Hilfesymbol
  UI/Pages/    Seiten des App-Fensters (Start, Wählen, Allgemein)
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

**Click to Call for Snom** dials phone numbers on Snom desk phones: click `tel:`/`callto:`/`sip:` links anywhere, or select a number in any application and press the hotkey (default `Ctrl + Alt + W`). Setup is guided: the app finds the phone on the network on first start, one click connects, and a short checklist covers the rest. It comes with number normalization, identity selection, hang-up button, update notifications and a German/English UI. Download the installer from [Releases](https://github.com/julianfaatz/click-to-call-for-snom/releases); no admin rights required.

## Lizenz

[MIT](LICENSE) © Julian Faatz
