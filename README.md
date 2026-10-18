<div align="center">

<img src="docs/logo.png" width="96" alt="">

# Click to Call

Telefonnummern am PC anklicken – das Snom- oder Yealink-Tischtelefon wählt.

[![Version](https://img.shields.io/github/v/release/julianfaatz/click-to-call?label=Version)](https://github.com/julianfaatz/click-to-call/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/julianfaatz/click-to-call/total?label=Downloads)](https://github.com/julianfaatz/click-to-call/releases)

**Deutsch** · [English](README.en.md)

</div>

Click to Call wählt Nummern vom Windows-PC aus auf deinem Tischtelefon – per Klick auf `tel:`-Links oder per Tastenkombination für markierte Nummern in jedem Programm. Die App steuert das Telefon direkt über dessen Web-Schnittstelle im lokalen Netzwerk, eine Telefonanlagen-Software oder ein Server ist nicht nötig.

![Startseite](docs/start.png)

## Funktionen

- 📞 `tel:`-, `callto:`- und `sip:`-Links öffnen am Telefon (Browser, Outlook, CRM …)
- ⌨️ Nummer markieren und <kbd>Strg</kbd>+<kbd>Alt</kbd>+<kbd>W</kbd> drücken – funktioniert auch in PDFs und E-Mails
- Rufnummern werden aufbereitet: `+49 (0)30 123 456-78` → `03012345678`, optional mit Amtsholung
- Rückfrage vor dem Wählen, Auflegen-Taste, Wahlwiederholung, Auswahl der Leitung
- Findet das Telefon selbst im Netzwerk
- Läuft im Infobereich der Taskleiste, Updates per Klick
- Deutsch und Englisch

<img src="docs/hotkey-popup.png" width="400" alt="Wählfenster nach Strg+Alt+W">

## Unterstützte Telefone

| Hersteller | Serien | Modelle (Beispiele) |
|---|---|---|
| **Snom** | D3xx, D7xx | D305, D315, D335, D345, D385, D713, D715, D717, D735, D785 |
| **Yealink** | T3x, T4x, T5x | T31, T33, T42, T43, T44, T46, T48, T53, T54, T57 |

Voraussetzung ist die HTTP-Fernsteuerung des Telefons: bei Snom über `command.htm`, bei Yealink über „Action URI“. Snom D8xx unterstützt diese Schnittstelle laut Hersteller nicht.

## Installation

1. `ClickToCall-x.y.z-setup.exe` unter [Releases](https://github.com/julianfaatz/click-to-call/releases/latest) herunterladen und ausführen (Windows 10/11, keine Admin-Rechte nötig). Alternativ gibt es eine portable ZIP-Datei.
2. Die App sucht das Telefon im Netzwerk. Gefundenes Telefon anklicken oder die IP-Adresse eingeben. Benötigt werden die Zugangsdaten der Web-Oberfläche des Telefons.
3. Der Checkliste auf der Startseite folgen. In den Windows-Einstellungen muss bei **TEL** einmal „Click to Call“ ausgewählt werden – das kann Windows nicht automatisch.

Der Installer ist nicht signiert. Zeigt Windows SmartScreen eine Warnung: *Weitere Informationen → Trotzdem ausführen*. Prüfsummen (SHA-256) liegen jedem Release bei.

<table>
  <tr>
    <td><img src="docs/waehlen1.png" alt="Einstellungen: Wählen und Hotkey"></td>
    <td><img src="docs/waehlen2.png" alt="Einstellungen: Links und Rufnummern"></td>
  </tr>
</table>

## Aus anderen Programmen wählen

Andere Software oder Skripte können die App wie einen Link aufrufen:

```powershell
ClickToCall.exe "tel:+49301234567"
```

## Fehlersuche

| Problem | Lösung |
|---|---|
| Telefon wird nicht gefunden | IP-Adresse von Hand eingeben (Telefonmenü → Informationen). Liegt das Telefon in einem anderen Netz, muss es vom PC aus erreichbar sein. |
| „Benutzername oder Passwort falsch“ | Zugangsdaten der Web-Oberfläche verwenden, nicht die SIP-Zugangsdaten. |
| „Das Telefon verweigert den Zugriff“ | Im Web-Interface prüfen, ob der Benutzer Admin-Rechte hat und HTTP-Steuerung erlaubt ist. |
| Yealink wählt nicht | Beim ersten Anruf „Fernsteuerung erlauben“ am Telefon bestätigen oder unter *Features → Remote Control* die IP des PCs bei **Action URI Allow IP List** eintragen. |
| Klick auf tel:-Link macht nichts | *Wählen → Links* aktivieren und in den Windows-Einstellungen „Click to Call“ auswählen. |
| Hotkey geht nicht | Unter *Wählen → Hotkey* eine andere Kombination wählen. |

Einstellungen und Protokoll liegen in `%APPDATA%\ClickToCall`. Hilft das nicht weiter: in der App unter *Allgemein → Fehler melden* oder direkt als [Issue](https://github.com/julianfaatz/click-to-call/issues/new/choose).

## Datenschutz

Die App verbindet sich nur mit dem Telefon und mit GitHub, um nach Updates zu suchen (abschaltbar). Es gibt keine Telemetrie. Das Telefon-Passwort wird mit Windows DPAPI verschlüsselt gespeichert.

## Mitmachen

Wie man die App baut, ohne Telefon testet und Releases erstellt, steht in [CONTRIBUTING.md](CONTRIBUTING.md). Änderungen zwischen den Versionen stehen im [Changelog](CHANGELOG.md).

## Lizenz

[MIT](LICENSE) © Julian Faatz

Kein offizielles Produkt von Snom oder Yealink. Snom und Yealink sind Marken ihrer jeweiligen Inhaber.
