using System.Globalization;
using System.Windows.Markup;

namespace ClickToCallForSnom.Core;

/// <summary>
/// Very small localization: German and English texts side by side.
/// In XAML: <c>Text="{loc:Tr Phone.Title}"</c>. Texts are resolved when a window is created.
/// </summary>
public static class Loc
{
    public static string Language { get; private set; } = "de";

    /// <param name="language">"de", "en" or empty for the Windows display language.</param>
    public static void SetLanguage(string? language)
    {
        Language = language switch
        {
            "de" or "en" => language,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? "de" : "en",
        };
    }

    public static string T(string key)
    {
        if (!Texts.TryGetValue(key, out var text))
            return key;
        return Language == "en" ? text.En : text.De;
    }

    public static string T(string key, params object[] args) => string.Format(T(key), args);

    private static readonly Dictionary<string, (string De, string En)> Texts = new()
    {
        // ---------- App ----------
        ["App.Description"] = ("Wählt tel:-Links und Rufnummern auf deinem Snom- oder Yealink-Tischtelefon.", "Dials tel: links and phone numbers on your Snom or Yealink desk phone."),
        ["App.Disclaimer"] = (
            "Kein offizielles Produkt von Snom oder Yealink. Snom und Yealink sind Marken ihrer jeweiligen Inhaber. Open Source unter MIT-Lizenz.",
            "Not an official product of Snom or Yealink. Snom and Yealink are trademarks of their respective owners. Open source under the MIT license."),

        // ---------- Common ----------
        ["Common.Close"] = ("Schließen", "Close"),
        ["Common.Cancel"] = ("Abbrechen", "Cancel"),
        ["Key.Ctrl"] = ("Strg", "Ctrl"),
        ["Key.Shift"] = ("Umschalt", "Shift"),
        ["Key.Space"] = ("Leertaste", "Space"),

        // ---------- Errors ----------
        ["Error.NotConfigured"] = ("Es ist noch kein Telefon verbunden.", "No phone has been connected yet."),
        ["Error.InvalidAddress"] = ("„{0}“ ist keine gültige IP-Adresse oder kein gültiger Name.", "\"{0}\" is not a valid IP address or host name."),
        ["Error.Unreachable"] = (
            "Telefon nicht erreichbar. Ist es eingeschaltet und die IP-Adresse richtig?",
            "Phone not reachable. Is it switched on and is the IP address correct?"),
        ["Error.Timeout"] = ("Das Telefon antwortet nicht.", "The phone does not respond."),
        ["Error.Unauthorized"] = ("Benutzername oder Passwort falsch.", "Wrong username or password."),
        ["Error.Forbidden"] = (
            "Das Telefon verweigert den Zugriff. Bitte im Web-Interface des Telefons prüfen, ob der Benutzer Admin-Rechte hat.",
            "The phone denied access. Please check in the phone's web interface that the user has admin rights."),
        ["Error.Certificate"] = (
            "Die sichere Verbindung (HTTPS) zum Telefon ist fehlgeschlagen. Bitte auf der Startseite neu verbinden.",
            "The secure connection (HTTPS) to the phone failed. Please connect again on the home page."),
        ["Error.HttpError"] = ("Das Telefon meldet einen Fehler (HTTP {0}).", "The phone reported an error (HTTP {0})."),

        // ---------- Phone specific ----------
        ["Yealink.RemoteControlHint"] = (
            "Beim ersten Anruf fragt das Telefon, ob es ferngesteuert werden darf – bitte mit OK bestätigen. "
                + "Passiert beim Wählen nichts: in der Web-Oberfläche des Telefons unter Features → Remote Control "
                + "bei „Action URI Allow IP List“ die IP-Adresse dieses PCs eintragen{0}.",
            "On the first call the phone asks whether it may be remote controlled – please confirm with OK. "
                + "If nothing happens when dialing: in the phone's web interface under Features → Remote Control, "
                + "add this PC's IP address{0} to the \"Action URI Allow IP List\"."),

        // ---------- Setup required ----------
        ["Setup.RequiredTitle"] = ("Noch kein Telefon verbunden", "No phone connected yet"),
        ["Setup.RequiredText"] = (
            "Verbinde zuerst dein Tischtelefon – danach geht es direkt mit deiner Nummer weiter.",
            "Connect your desk phone first – then you'll continue right where you left off."),

        // ---------- Dial window ----------
        ["Dial.WindowTitle"] = ("Nummer wählen", "Dial number"),
        ["Dial.Title"] = ("Nummer wählen", "Dial number"),
        ["Dial.TitleLink"] = ("Diese Nummer wählen?", "Dial this number?"),
        ["Dial.Identity"] = ("Leitung", "Line"),
        ["Dial.Dial"] = ("Wählen (Enter)", "Dial (Enter)"),
        ["Dial.Cancel"] = ("Abbrechen (Esc)", "Cancel (Esc)"),
        ["Dial.Preview"] = ("Wird gewählt als: {0}", "Will be dialed as: {0}"),
        ["Dial.Recent"] = ("Zuletzt gewählt", "Recently dialed"),

        // ---------- Call ----------
        ["Call.InvalidTitle"] = ("Keine gültige Rufnummer", "Not a valid phone number"),
        ["Call.Dialing"] = ("Wähle …", "Dialing …"),
        ["Call.Dialed"] = ("Telefon wählt", "Phone is dialing"),
        ["Call.HangUp"] = ("Auflegen", "Hang up"),
        ["Call.Failed"] = ("Wählen fehlgeschlagen", "Dialing failed"),
        ["Call.Retry"] = ("Erneut versuchen", "Retry"),
        ["Call.CheckPhone"] = ("Verbindung prüfen", "Check connection"),
        ["Call.HangUpFailed"] = ("Auflegen fehlgeschlagen", "Hang up failed"),

        // ---------- Connect form ----------
        ["Phone.Host"] = ("IP-Adresse", "IP address"),
        ["Phone.HostHelp"] = (
            "Steht im Telefonmenü unter Informationen bzw. Status, z. B. 192.168.1.50.",
            "Shown in the phone's menu under Information or Status, e.g. 192.168.1.50."),
        ["Phone.Vendor"] = ("Hersteller", "Manufacturer"),
        ["Phone.VendorAuto"] = ("Automatisch erkennen", "Recognize automatically"),
        ["Phone.Username"] = ("Benutzername", "Username"),
        ["Phone.Password"] = ("Passwort", "Password"),
        ["Phone.Testing"] = ("Verbinde …", "Connecting …"),

        // ---------- Identities ----------
        ["Identity.Title"] = ("Leitungen", "Lines"),
        ["Identity.Intro"] = (
            "Werden beim Verbinden automatisch vom Telefon gelesen. Nur wichtig, wenn dein Telefon mehrere Leitungen (Identitäten) hat.",
            "Read from the phone automatically when connecting. Only relevant if your phone has several lines (identities)."),
        ["Identity.Count"] = (
            "{0} Leitung(en) eingerichtet. Die Standard-Leitung wählst du auf der Startseite.",
            "{0} line(s) set up. Pick the default line on the home page."),
        ["Identity.Load"] = ("Neu einlesen", "Reload"),
        ["Identity.Loading"] = ("Lese Leitungen vom Telefon …", "Reading lines from the phone …"),
        ["Identity.Loaded"] = ("{0} Leitung(en) gefunden", "{0} line(s) found"),
        ["Identity.NoneFound"] = ("Keine Leitungen gefunden.", "No lines found."),
        ["Identity.IntroManual"] = (
            "Nur wichtig, wenn dein Telefon mehrere Leitungen (Identitäten) hat. Dieses Telefon meldet sie nicht – bitte unten manuell hinzufügen.",
            "Only relevant if your phone has several lines (identities). This phone does not report them – please add them manually below."),
        ["Identity.ManualFallback"] = ("Du kannst Leitungen auch manuell hinzufügen.", "You can also add lines manually."),
        ["Identity.ParseError"] = ("Die Antwort des Telefons konnte nicht gelesen werden. Bitte manuell hinzufügen.", "Could not read the phone's response. Please add lines manually."),
        ["Identity.PhoneDefault"] = ("Standard des Telefons", "Phone default"),
        ["Identity.Numbered"] = ("Identität {0}", "Identity {0}"),
        ["Identity.Remove"] = ("Entfernen", "Remove"),
        ["Identity.Add"] = ("Hinzufügen", "Add"),
        ["Identity.Invalid"] = ("Bitte eine SIP-Adresse wie 1234@pbx.firma.de eingeben.", "Please enter a SIP address like 1234@pbx.example.com."),
        ["Identity.ManualHint"] = ("Manuell hinzufügen: SIP-Adresse der Leitung, z. B. 1234@pbx.firma.de", "Add manually: SIP address of the line, e.g. 1234@pbx.example.com"),
        ["Identity.ShowSelection"] = ("Leitung bei jedem Anruf auswählen", "Choose the line for every call"),
        ["Identity.ShowSelectionHelp"] = ("Das Wählfenster fragt, über welche Leitung gewählt werden soll.", "The dial window asks which line to use."),

        // ---------- Dialing page ----------
        ["Dialing.Title"] = ("Wählen", "Dialing"),
        ["Dialing.ConfirmLinks"] = ("Vor dem Wählen nachfragen", "Ask before dialing"),
        ["Dialing.ConfirmLinksHint"] = (
            "Bei Klick auf einen Telefon-Link erst die Nummer zeigen – Enter wählt, Esc bricht ab.",
            "When clicking a phone link, show the number first – Enter dials, Esc cancels."),
        ["Dialing.ShowNotification"] = ("Benachrichtigung mit Auflegen-Taste", "Notification with hang-up button"),
        ["Dialing.ShowNotificationHint"] = ("Erscheint unten rechts, während das Telefon wählt.", "Appears in the bottom right corner while the phone dials."),
        ["Dialing.SecondsOption"] = ("{0} Sekunden", "{0} seconds"),
        ["Dialing.NormalizeTitle"] = ("Rufnummern", "Phone numbers"),
        ["Dialing.ConvertOwnCountry"] = ("Eigene Landesvorwahl durch 0 ersetzen", "Replace own country code with 0"),
        ["Dialing.ConvertOwnCountryHelp"] = ("Aus +{0} 30 1234 wird 030 1234.", "Turns +{0} 30 1234 into 030 1234."),
        ["Dialing.PlusToZeros"] = ("Bei Auslandsnummern + durch 00 ersetzen", "Replace + with 00 for foreign numbers"),
        ["Dialing.PlusToZerosHelp"] = (
            "Aus +43 1 2345 wird 0043 1 2345. Für Telefonanlagen, die kein + wählen können.",
            "Turns +43 1 2345 into 0043 1 2345. For phone systems that cannot dial a +."),
        ["Dialing.OutsideLine"] = ("Amtsholung", "Outside line prefix"),
        ["Dialing.OutsideLineHint"] = (
            "Ziffer vor externen Nummern, z. B. 0. Leer lassen, wenn deine Telefonanlage keine braucht.",
            "Digit dialed before external numbers, e.g. 9. Leave empty if your phone system doesn't need one."),
        ["Dialing.OutsideLineFrom"] = ("Nur bei Nummern ab", "Only for numbers with at least"),
        ["Dialing.Digits"] = ("Ziffern – kürzere sind interne Durchwahlen", "digits – shorter ones are internal extensions"),
        ["Dialing.TryIt"] = ("Ausprobieren: So wird eine Nummer gewählt", "Try it: this is how a number gets dialed"),

        // ---------- Hotkey ----------
        ["Hotkey.Title"] = ("Hotkey", "Hotkey"),
        ["Hotkey.Intro"] = (
            "Nummer in einem beliebigen Programm markieren und Tastenkombination drücken – das Wählfenster öffnet sich.",
            "Select a number in any application and press the key combination – the dial window opens."),
        ["Hotkey.Enabled"] = ("Wählen per Tastenkombination", "Dial with a key combination"),
        ["Hotkey.Combination"] = ("Tastenkombination", "Key combination"),
        ["Hotkey.BoxHint"] = ("Ins Feld klicken und die neue Kombination drücken.", "Click the box and press the new combination."),
        ["Hotkey.Reset"] = ("Auf Standard zurücksetzen", "Reset to default"),
        ["Hotkey.Recording"] = ("Jetzt Tasten drücken …", "Press the keys now …"),
        ["Hotkey.Available"] = ("Aktiv", "Active"),
        ["Hotkey.Saved"] = ("Gespeichert", "Saved"),
        ["Hotkey.InUse"] = ("Wird schon von einem anderen Programm verwendet", "Already used by another application"),
        ["Hotkey.NeedModifier"] = ("Bitte mit Strg, Alt oder Win kombinieren", "Please combine with Ctrl, Alt or Win"),
        ["Hotkey.ClipboardFallback"] = ("Kopierte Nummer übernehmen", "Use a copied number"),
        ["Hotkey.ClipboardFallbackHelp"] = (
            "Ist nichts markiert, wird eine Nummer aus der Zwischenablage ins Wählfenster übernommen.",
            "If nothing is selected, a number from the clipboard is put into the dial window."),
        ["Hotkey.InUseTitle"] = ("Hotkey nicht verfügbar", "Hotkey not available"),
        ["Hotkey.InUseText"] = (
            "{0} wird bereits von einem anderen Programm verwendet. Bitte wähle eine andere Tastenkombination.",
            "{0} is already used by another application. Please choose a different combination."),

        // ---------- Links ----------
        ["Protocols.Title"] = ("Links", "Links"),
        ["Protocols.DefaultTitle"] = ("Telefon-Links mit Click to Call öffnen", "Open phone links with Click to Call"),
        ["Protocols.Tel"] = ("Telefonnummern auf Webseiten, in E-Mails, im CRM", "Phone numbers on websites, in e-mails, in CRMs"),
        ["Protocols.Callto"] = ("Ältere Variante, z. B. in manchen CRM-Systemen", "Older variant, e.g. in some CRM systems"),
        ["Protocols.Sip"] = ("SIP-Adressen", "SIP addresses"),
        ["Protocols.OpenDefaultApps"] = ("Windows-Einstellungen", "Windows settings"),
        ["Protocols.IsDefault"] = ("Aktiv", "Active"),
        ["Protocols.NotDefault"] = ("Nicht aktiv", "Not active"),

        // ---------- General page ----------
        ["General.Title"] = ("Allgemein", "General"),
        ["General.Language"] = ("Sprache", "Language"),
        ["General.LanguageSystem"] = ("Wie Windows", "Same as Windows"),
        ["General.Autostart"] = ("Mit Windows starten", "Start with Windows"),
        ["General.AutostartHelp"] = (
            "Damit Links und Hotkey nach jedem Neustart sofort funktionieren. Die App startet unsichtbar im Infobereich.",
            "So that links and the hotkey work right after every restart. The app starts hidden in the notification area."),
        ["General.Installed"] = ("Installiert: Version {0}", "Installed: version {0}"),
        ["General.UpdatesTitle"] = ("Updates", "Updates"),
        ["General.CheckUpdates"] = ("Automatisch nach neuen Versionen suchen", "Automatically check for new versions"),
        ["General.CheckNow"] = ("Jetzt suchen", "Check now"),
        ["General.Advanced"] = ("Erweitert", "Advanced"),
        ["General.Troubleshooting"] = ("Fehlersuche", "Troubleshooting"),
        ["General.TroubleshootingHint"] = ("Das Protokoll hilft, wenn etwas nicht klappt.", "The log helps when something doesn't work."),
        ["General.OpenLog"] = ("Protokoll öffnen", "Open log"),
        ["General.OpenFolder"] = ("Ordner öffnen", "Open folder"),

        // ---------- Updates ----------
        ["Update.Checking"] = ("Suche …", "Checking …"),
        ["Update.UpToDate"] = ("Du verwendest die neueste Version", "You are using the latest version"),
        ["Update.UpToDateDetail"] = ("Version {0} · {1}", "Version {0} · {1}"),
        ["Update.CheckedToday"] = ("heute um {0} geprüft", "checked today at {0}"),
        ["Update.CheckedOn"] = ("geprüft am {0}", "checked on {0}"),
        ["Update.CheckFailedDetail"] = (
            "GitHub war nicht erreichbar. Bitte später erneut prüfen – deine Version funktioniert weiter.",
            "GitHub could not be reached. Please check again later – your version keeps working."),
        ["Update.NotChecked"] = ("Automatische Prüfung ist ausgeschaltet", "Automatic checking is turned off"),
        ["Update.Available"] = ("Version {0} ist verfügbar", "Version {0} is available"),
        ["Update.CheckFailed"] = ("Update-Prüfung fehlgeschlagen", "Update check failed"),
        ["Update.AvailableTitle"] = ("Update verfügbar", "Update available"),
        ["Update.AvailableText"] = ("Version {0} ist verfügbar (installiert: {1}).", "Version {0} is available (installed: {1})."),
        ["Update.Install"] = ("Installieren", "Install"),
        ["Update.Details"] = ("Details", "Details"),
        ["Update.Skip"] = ("Überspringen", "Skip"),
        ["Update.Downloading"] = ("Update wird heruntergeladen …", "Downloading update …"),
        ["Update.Failed"] = ("Update fehlgeschlagen", "Update failed"),

        // ---------- Navigation ----------
        ["Nav.Home"] = ("Start", "Home"),
        ["Nav.Dialing"] = ("Wählen", "Dialing"),
        ["Nav.General"] = ("Allgemein", "General"),

        // ---------- Home ----------
        ["Home.Title"] = ("Start", "Home"),
        ["Home.NoPhone"] = ("Telefon verbinden", "Connect your phone"),
        ["Home.NoPhoneText"] = ("Einmal einrichten, dann wählst du per Klick.", "Set it up once, then dial with a click."),
        ["Home.DoConnect"] = ("Verbinden", "Connect"),
        ["Home.Change"] = ("Ändern", "Change"),
        ["Home.Recheck"] = ("Verbindung erneut prüfen", "Check connection again"),
        ["Home.Online"] = ("Verbunden", "Connected"),
        ["Home.Offline"] = ("Nicht erreichbar", "Not reachable"),
        ["Home.Checking"] = ("Prüfe Verbindung …", "Checking connection …"),
        ["Home.ConnectedTitle"] = ("Telefon verbunden", "Phone connected"),
        ["Home.ConnectedText"] = ("{0} ist bereit.", "{0} is ready."),
        ["Home.HostRequired"] = ("Bitte die IP-Adresse des Telefons eingeben.", "Please enter the phone's IP address."),
        ["Home.Scan"] = ("Im Netzwerk suchen", "Search network"),
        ["Home.ScanAgain"] = ("Erneut suchen", "Search again"),
        ["Home.Scanning"] = ("Suche dein Snom- oder Yealink-Telefon im Netzwerk …", "Looking for your Snom or Yealink phone on the network …"),
        ["Home.ScanFoundOne"] = ("Gefunden! Klicke auf dein Telefon, um es zu verbinden:", "Found it! Click your phone to connect:"),
        ["Home.ScanFound"] = ("Mehrere Telefone gefunden – klicke auf deins:", "Several phones found – click yours:"),
        ["Home.ScanNone"] = ("Kein Snom- oder Yealink-Telefon gefunden. Bitte die IP-Adresse oben eintragen.", "No Snom or Yealink phone found. Please enter the IP address above."),
        ["Home.VendorUnknown"] = (
            "Unter dieser Adresse antwortet ein Gerät, der Hersteller wurde aber nicht erkannt. Bitte auswählen und erneut verbinden.",
            "A device answers at this address, but its manufacturer was not recognized. Please select it and connect again."),
        ["Home.NeedsLogin"] = (
            "Dein Telefon ist passwortgeschützt. Bitte die Zugangsdaten der Web-Oberfläche eingeben (nicht die SIP-Daten).",
            "Your phone is password protected. Please enter the web interface credentials (not the SIP credentials)."),
        ["Home.Line"] = ("Leitung", "Line"),
        ["Home.LineHelp"] = (
            "Dein Telefon hat mehrere Leitungen (Identitäten). Hier legst du fest, über welche standardmäßig gewählt wird.",
            "Your phone has several lines (identities). Choose which one is used for dialing by default."),
        ["Home.SetupTitle"] = ("Einrichtung", "Setup"),
        ["Home.SetupSubtitle"] = ("In drei Schritten startklar – dauert etwa eine Minute.", "Ready in three steps – takes about a minute."),
        ["Home.Next"] = ("Weiter", "Next"),
        ["Home.SkipStep"] = ("Überspringen", "Skip"),
        ["Home.Skipped"] = ("(übersprungen)", "(skipped)"),
        ["Home.StepGoTo"] = ("Zu diesem Schritt", "Go to this step"),
        ["Home.StepPhone"] = ("Telefon verbinden", "Connect phone"),
        ["Home.StepLinksShort"] = ("Links aktivieren", "Activate links"),
        ["Home.StepLinks"] = ("Telefonnummern anklickbar machen", "Make phone numbers clickable"),
        ["Home.StepLinksText"] = (
            "Dann wählt ein Klick auf eine Telefonnummer – im Browser, in Outlook oder im CRM – direkt auf deinem Telefon.",
            "Then a click on a phone number – in the browser, in Outlook or in your CRM – dials right on your phone."),
        ["Home.StepLinksHow1"] = ("Auf „Windows-Einstellungen öffnen“ klicken", "Click \"Open Windows settings\""),
        ["Home.StepLinksHow2"] = ("Bei TEL „Click to Call for Phones“ auswählen", "Pick \"Click to Call for Phones\" for TEL"),
        ["Home.StepLinksHow3"] = ("Hierher zurückkommen – es geht automatisch weiter", "Come back here – the setup continues automatically"),
        ["Home.StepLinksButton"] = ("Windows-Einstellungen öffnen", "Open Windows settings"),
        ["Home.StepLinksButtonAgain"] = ("Erneut öffnen", "Open again"),
        ["Home.StepLinksWaiting"] = (
            "Noch nicht erkannt. Bitte in den Windows-Einstellungen bei TEL „Click to Call for Phones“ auswählen.",
            "Not detected yet. Please pick \"Click to Call for Phones\" for TEL in the Windows settings."),
        ["Home.StepLinksDone"] = ("✓ Erledigt – Telefonnummern sind jetzt anklickbar.", "✓ Done – phone numbers are now clickable."),
        ["Home.StepLinksTodo"] = (
            "Öffnet die Windows-Einstellungen – dort bei TEL „Click to Call for Phones“ auswählen.",
            "Opens the Windows settings – pick \"Click to Call for Phones\" for TEL there."),
        ["Home.StepTest"] = ("Testanruf", "Test call"),
        ["Home.TestQuestion"] = ("Wählt dein Telefon jetzt {0}?", "Is your phone dialing {0} now?"),
        ["Home.TestYes"] = ("Ja, es wählt", "Yes, it's dialing"),
        ["Home.TestNo"] = ("Nein", "No"),
        ["Home.TestFailedTitle"] = ("Daran kann es liegen:", "Possible causes:"),
        ["Home.TestFailedNumber"] = (
            "Das Telefon hat die Nummer {0} bekommen. Braucht deine Telefonanlage für Gespräche nach draußen eine Amtsholung (z. B. eine 0 davor), lässt sich das in den Rufnummern-Einstellungen eintragen.",
            "The phone received the number {0}. If your PBX needs a prefix for outside calls (e.g. a leading 0), you can set it in the number settings."),
        ["Home.TestRetry"] = ("Erneut versuchen", "Try again"),
        ["Home.TestNumberSettings"] = ("Rufnummern-Einstellungen", "Number settings"),
        ["Home.StepTestText"] = (
            "Gib z. B. deine Handynummer ein. Dein Tischtelefon wählt sofort – wenn dein Handy klingelt, klappt alles.",
            "Enter e.g. your mobile number. Your desk phone dials right away – if your mobile rings, everything works."),
        ["Home.SkipTest"] = ("Überspringen und fertigstellen", "Skip and finish"),
        ["Home.DoneTitle"] = ("Alles eingerichtet!", "All set!"),
        ["Home.DoneText"] = ("Click to Call ist startklar – viel Spaß beim Telefonieren.", "Click to Call is ready – enjoy calling."),
        ["Home.ReadyTitle"] = ("Alles bereit – so wählst du", "All set – here's how to dial"),
        ["Home.DialTitle"] = ("Nummer eingeben", "Enter a number"),
        ["Home.DialDescription"] = ("Nummer eintippen oder einfügen und Enter drücken.", "Type or paste a number and press Enter."),
        ["Home.LinksTitle"] = ("Auf Telefonnummern klicken", "Click phone numbers"),
        ["Home.LinksActive"] = (
            "Im Browser, in Outlook oder im CRM – dein Telefon wählt sofort.",
            "In the browser, in Outlook or in your CRM – your phone dials right away."),
        ["Home.LinksSet"] = ("Aktivieren", "Activate"),
        ["Home.HotkeyTitle"] = ("Nummer markieren und {0} drücken", "Select a number and press {0}"),
        ["Home.HotkeyDescription"] = ("Funktioniert in jedem Programm – auch in PDFs und E-Mails.", "Works in any application – including PDFs and e-mails."),
        ["Home.HotkeyOffTitle"] = ("Wählen per Tastenkombination", "Dial with a key combination"),
        ["Home.HotkeyOff"] = ("Ausgeschaltet", "Turned off"),
        ["Home.TrayTitle"] = ("Immer griffbereit in der Taskleiste", "Always at hand in the taskbar"),
        ["Home.TrayDescription"] = (
            "Rechtsklick auf das Symbol unten rechts: Nummer wählen, Wahlwiederholung, Auflegen – auch wenn dieses Fenster zu ist.",
            "Right-click the icon in the bottom right corner: dial, redial, hang up – even when this window is closed."),
        ["Home.Call"] = ("Anrufen", "Call"),

        // ---------- Tray ----------
        ["Tray.Dial"] = ("Nummer wählen …", "Dial number …"),
        ["Tray.Redial"] = ("Wahlwiederholung", "Redial"),
        ["Tray.RedialNumber"] = ("Wahlwiederholung: {0}", "Redial: {0}"),
        ["Tray.Open"] = ("Click to Call öffnen", "Open Click to Call"),
        ["Tray.CheckUpdates"] = ("Nach Updates suchen", "Check for updates"),
        ["Tray.Exit"] = ("Beenden", "Exit"),

        // ---------- Ready ----------
        ["Ready.Title"] = ("Click to Call läuft weiter", "Click to Call keeps running"),
        ["Ready.Background"] = (
            "Die App wartet unten rechts im Infobereich der Taskleiste. Links und Hotkey funktionieren weiter.",
            "The app waits in the notification area of the taskbar. Links and the hotkey keep working."),
    };
}

/// <summary>XAML markup extension: <c>{loc:Tr Some.Key}</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
