# Changelog

Alle nennenswerten Änderungen an Click to Call. Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), Versionsnummern nach [Semantic Versioning](https://semver.org/lang/de/).

Der Abschnitt einer Version wird beim Release automatisch als Release-Text auf GitHub übernommen.

## [Unreleased]

## [0.9.3] - 2026-10-24

### Behoben

- Snom mit Web-Login: Das Telefon wählte, die App meldete aber „Benutzername oder Passwort falsch“ – leitet das Telefon nach dem Befehl weiter, wird der Login jetzt mitgeschickt

## [0.9.2] - 2026-10-19

### Behoben

- Snom: Lehnt das Telefon das Wählen ab, weil „Use hidden security tags“ aktiv ist, erklärt die Fehlermeldung jetzt, was zu tun ist, statt „Telefon nicht erreichbar“ anzuzeigen
- Die Einrichtung überspringt den Schritt „Telefonnummern anklickbar machen“ nicht mehr, solange Click to Call in den Windows-Einstellungen nicht als Standard-App gewählt ist
- Die Telefonsuche findet Telefone auch in Netzen, die größer als /24 sind (bis /22)

### Geändert

- Die Telefonsuche durchsucht zusätzlich die Standardnetze gängiger Router (FRITZ!Box `192.168.178.x`, `192.168.0.x`, `192.168.1.x`, Speedport `192.168.2.x`) – z. B. für Telefone an einer FRITZ!Box als Telefonanlage, während der PC in einem anderen Netz hängt
- Die Telefonsuche zeigt als Radar live, welche Geräte antworten und welche Telefone gefunden wurden – ein gefundenes Telefon lässt sich direkt anklicken

## [0.9.1] - 2026-10-18

### Hinzugefügt

- „Fehler melden“ unter *Allgemein* öffnet ein vorausgefülltes Fehlerformular auf GitHub
- SHA-256-Prüfsummen für die Release-Dateien

## [0.9.0] - 2026-10-18

Erste öffentliche Version.

### Hinzugefügt

- Wählen auf **Snom**- und **Yealink**-Tischtelefonen über deren HTTP-Schnittstelle
- `tel:`-, `callto:`- und `sip:`-Links öffnen direkt am Telefon
- Hotkey (Standard `Strg + Alt + W`) wählt markierte Nummern in jedem Programm
- Bestätigungsfenster vor dem Wählen, für Links und markierte Nummern getrennt abschaltbar
- Benachrichtigung mit Auflegen-Taste, Wahlwiederholung
- Rufnummern-Aufbereitung mit optionaler Amtsholung
- Identitätsauswahl bei mehreren Leitungen
- Geführte Einrichtung: Telefonsuche im Netzwerk, automatische Erkennung von Hersteller, HTTP/HTTPS und Login
- Deutsche und englische Oberfläche
- Update-Hinweis und Installation neuer Versionen per Klick
- Installer ohne Admin-Rechte und portable ZIP

[Unreleased]: https://github.com/julianfaatz/click-to-call/compare/v0.9.3...HEAD
[0.9.3]: https://github.com/julianfaatz/click-to-call/compare/v0.9.2...v0.9.3
[0.9.2]: https://github.com/julianfaatz/click-to-call/compare/v0.9.1...v0.9.2
[0.9.1]: https://github.com/julianfaatz/click-to-call/compare/v0.9.0...v0.9.1
[0.9.0]: https://github.com/julianfaatz/click-to-call/releases/tag/v0.9.0
