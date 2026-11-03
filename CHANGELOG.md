# Changelog

Alle nennenswerten Änderungen an Click to Call. Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), Versionsnummern nach [Semantic Versioning](https://semver.org/lang/de/).

Der Abschnitt einer Version wird beim Release automatisch als Release-Text auf GitHub übernommen.

## [Unreleased]

### Hinzugefügt

- Einrichtung mit neuem Schritt „Tastenkombination“: Nummer in einer Beispiel-Signatur markieren, <kbd>Strg</kbd>+<kbd>Alt</kbd>+<kbd>W</kbd> drücken – die App erkennt es und lobt, ohne wirklich zu wählen. Ist nichts oder nur ein Teil markiert, erklärt sie, was zu tun ist
- Wählfenster: Wurde per Tastenkombination keine Nummer markiert, erklärt ein Hinweis, wie es gedacht ist
- Beim Festlegen einer Tastenkombination zeigt das Feld schon die gedrückten Tasten („Strg + Alt + …“). Kombinationen, die ein Zeichen tippen (z. B. <kbd>Strg</kbd>+<kbd>Alt</kbd>+<kbd>Q</kbd> = @ auf deutschen Tastaturen), werden abgelehnt
- Der Tooltip des Symbols im Infobereich nennt die Tastenkombination zum Wählen
- „Auf Standard zurücksetzen“ unten auf der Seite „Wählen“: setzt Rückfrage, Benachrichtigung, Tastenkombinationen, Link-Arten und Rufnummern-Format zurück, das Telefon bleibt verbunden. Mit „Rückgängig“ lässt es sich sofort zurücknehmen
- Auflegen per Tastenkombination (Standard <kbd>Strg</kbd>+<kbd>Alt</kbd>+<kbd>A</kbd>) – funktioniert in jedem Programm, lässt sich unter *Wählen → Tastenkombinationen* ändern oder abschalten und steht im Menü des Infobereichs
- Das Protokoll hält fest, woran das manuelle Verbinden scheitert und welche Geräte die Telefonsuche gefunden, aber nicht als Telefon erkannt hat (Server, Login, Seitentitel) – damit lassen sich nicht erkannte Telefone eingrenzen

### Geändert

- Telefon verbinden: Statt eines immer länger werdenden Formulars zeigt die Einrichtung eine Ansicht nach der anderen – Suche, IP-Adresse, Anmeldung, Hersteller. Gefundene Telefone erscheinen schon während der Suche als Kacheln, die Anmeldung erscheint an Ort und Stelle statt unterhalb des Radars, Fehler stehen direkt an der Aktion. Nach dem Verbinden bestätigt die Karte den Erfolg und es geht mit „Weiter“ zum nächsten Schritt
- Erfolgs- und Fehlermeldungen zeigen ein Windows-Symbol statt „✓“ / „✗“
- Neues Symbol für „Telefonnummern anklickbar machen“: eine unterstrichene Nummer, auf die getippt wird
- „Kopierte Nummer übernehmen“ ist für neue Installationen eingeschaltet: Ist nichts markiert, steht eine kopierte Nummer im Wählfenster (gewählt wird erst nach Bestätigung)
- Seite „Wählen“ aufgeräumt: Link-Arten (tel:, callto:, sip:) und Rufnummern-Format stehen unter „Erweiterte Einstellungen anzeigen“
- Texte: „Tastenkombination“ statt „Hotkey“; Hinweis, dass das Symbol im Infobereich hinter dem Pfeil ˄ liegen kann
- Yealink T4xS (z. B. T48S): Die Erkennung nutzt zusätzlich die Modellangabe der Weboberfläche, falls die Startseite „Yealink“ nicht nennt

### Behoben

- Yealink T4xS (z. B. T48S): Telefone, deren Startseite nur auf die HTTPS-Anmeldeseite weiterleitet, werden jetzt erkannt – auch wenn die Anmeldeseite selbst nicht antwortet
- Yealink: Bricht das Telefon die HTTPS-Verbindung mit Windows ab, verbindet die App jetzt über HTTP, über das auch das Wählen funktioniert. Ein falsches Passwort zeigt sich dann erst beim ersten Anruf
- Yealink: Verlangt das Telefon eine Digest-Anmeldung, meldet die App nicht mehr fälschlich „Benutzername oder Passwort falsch“

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
