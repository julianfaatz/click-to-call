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
        ["App.Description"] = ("Wählt tel:-Links und Rufnummern auf Snom-Tischtelefonen.", "Dials tel: links and phone numbers on Snom desk phones."),
        ["App.Disclaimer"] = (
            "Kein offizielles Produkt der Snom Technology GmbH. Snom ist eine Marke der Snom Technology GmbH. Open Source unter MIT-Lizenz.",
            "Not an official product of Snom Technology GmbH. Snom is a trademark of Snom Technology GmbH. Open source under the MIT license."),

        // ---------- Common ----------
        ["Common.Close"] = ("Schließen", "Close"),
        ["Common.Save"] = ("Speichern", "Save"),
        ["Common.Cancel"] = ("Abbrechen", "Cancel"),
        ["Key.Ctrl"] = ("Strg", "Ctrl"),
        ["Key.Shift"] = ("Umschalt", "Shift"),
        ["Key.Space"] = ("Leertaste", "Space"),

        // ---------- Errors ----------
        ["Error.NotConfigured"] = ("Es ist noch kein Telefon eingerichtet.", "No phone has been set up yet."),
        ["Error.Unreachable"] = ("Telefon nicht erreichbar. Bitte IP-Adresse und Netzwerk prüfen.", "Phone not reachable. Please check the IP address and network."),
        ["Error.Timeout"] = ("Das Telefon antwortet nicht (Zeitüberschreitung).", "The phone does not respond (timeout)."),
        ["Error.Unauthorized"] = ("Benutzername oder Passwort falsch.", "Wrong username or password."),
        ["Error.Forbidden"] = (
            "Das Telefon verweigert den Zugriff (403). Bitte Benutzerrechte und die HTTP-Fernsteuerung im Telefon prüfen.",
            "The phone denied access (403). Please check the user's rights and the HTTP remote control setting on the phone."),
        ["Error.Certificate"] = (
            "Das HTTPS-Zertifikat des Telefons wurde abgelehnt. Option „Zertifikat nicht prüfen“ aktivieren oder HTTP verwenden.",
            "The phone's HTTPS certificate was rejected. Enable \"Don't verify certificate\" or use HTTP."),
        ["Error.HttpError"] = ("Das Telefon meldet einen Fehler (HTTP {0}).", "The phone reported an error (HTTP {0})."),

        // ---------- Dial window ----------
        ["Dial.WindowTitle"] = ("Nummer wählen", "Dial number"),
        ["Dial.Title"] = ("Nummer wählen", "Dial number"),
        ["Dial.TitleLink"] = ("Diese Nummer wählen?", "Dial this number?"),
        ["Dial.Identity"] = ("Identität", "Identity"),
        ["Dial.Dial"] = ("Wählen (Enter)", "Dial (Enter)"),
        ["Dial.Cancel"] = ("Abbrechen (Esc)", "Cancel (Esc)"),
        ["Dial.Preview"] = ("Wird gewählt als: {0}", "Will be dialed as: {0}"),

        // ---------- Call ----------
        ["Call.InvalidTitle"] = ("Keine gültige Rufnummer", "Not a valid phone number"),
        ["Call.Dialing"] = ("Wähle …", "Dialing …"),
        ["Call.Dialed"] = ("Telefon wählt", "Phone is dialing"),
        ["Call.HangUp"] = ("Auflegen", "Hang up"),
        ["Call.Failed"] = ("Wählen fehlgeschlagen", "Dialing failed"),
        ["Call.Retry"] = ("Erneut versuchen", "Retry"),
        ["Call.HangUpFailed"] = ("Auflegen fehlgeschlagen", "Hang up failed"),

        // ---------- Phone page ----------
        ["Phone.Title"] = ("Telefon", "Phone"),
        ["Phone.Intro"] = (
            "Die App steuert dein Snom-Telefon über dessen Web-Oberfläche. Trage die IP-Adresse des Telefons und die Zugangsdaten der Web-Oberfläche ein. Die IP-Adresse findest du am Telefon meist unter Menü → Informationen.",
            "The app controls your Snom phone through its web interface. Enter the phone's IP address and the web interface credentials. You can usually find the IP address on the phone under Menu → Information."),
        ["Phone.Host"] = ("IP-Adresse", "IP address"),
        ["Phone.Connection"] = ("Verbindung", "Connection"),
        ["Phone.UseHttps"] = ("HTTPS verwenden", "Use HTTPS"),
        ["Phone.IgnoreCert"] = ("Zertifikat nicht prüfen (selbstsigniert)", "Don't verify certificate (self-signed)"),
        ["Phone.Username"] = ("Benutzername", "Username"),
        ["Phone.Password"] = ("Passwort", "Password"),
        ["Phone.HostHelp"] = (
            "Die IP-Adresse findest du am Telefon unter Menü → Informationen (oder Taste „i“), z. B. 192.168.1.50.",
            "You can find the IP address on the phone under Menu → Information (or the \"i\" key), e.g. 192.168.1.50."),
        ["Phone.ConnectionHelp"] = (
            "Beim Verbinden auf der Startseite wird automatisch erkannt, ob das Telefon HTTP oder HTTPS nutzt. Snom-Telefone verwenden selbstsignierte Zertifikate, deshalb wird das Zertifikat standardmäßig nicht geprüft.",
            "Connecting on the home page detects automatically whether the phone uses HTTP or HTTPS. Snom phones use self-signed certificates, so the certificate is not verified by default."),
        ["Phone.CredentialsHelp"] = (
            "Die Zugangsdaten, mit denen du dich im Browser an der Web-Oberfläche des Telefons anmeldest – nicht die SIP-Zugangsdaten. Das Passwort wird verschlüsselt gespeichert.",
            "The credentials you use to log in to the phone's web interface in the browser – not the SIP credentials. The password is stored encrypted."),
        ["Phone.Test"] = ("Verbindung testen", "Test connection"),
        ["Phone.Testing"] = ("Verbinde …", "Connecting …"),
        ["Phone.TestOk"] = ("Verbunden: {0}", "Connected: {0}"),

        // ---------- Identities ----------
        ["Identity.Title"] = ("Identität (Leitung)", "Identity (line)"),
        ["Identity.Intro"] = (
            "Normalerweise wählt das Telefon über seine Standard-Identität. Wenn mehrere Identitäten eingerichtet sind, kannst du sie hier vom Telefon laden und eine als Standard festlegen oder im Wählfenster zur Auswahl anbieten.",
            "Normally the phone dials using its default identity. If several identities are configured, you can load them from the phone here and pick a default or offer a choice in the dial window."),
        ["Identity.Load"] = ("Vom Telefon laden", "Load from phone"),
        ["Identity.Loading"] = ("Lade …", "Loading …"),
        ["Identity.Loaded"] = ("{0} Identität(en) gefunden", "{0} identity(ies) found"),
        ["Identity.NoneFound"] = ("Keine Identitäten gefunden.", "No identities found."),
        ["Identity.ManualFallback"] = ("Du kannst Identitäten auch manuell hinzufügen.", "You can also add identities manually."),
        ["Identity.ParseError"] = ("Die Antwort des Telefons konnte nicht gelesen werden. Bitte manuell hinzufügen.", "Could not read the phone's response. Please add identities manually."),
        ["Identity.Default"] = ("Standard-Identität", "Default identity"),
        ["Identity.PhoneDefault"] = ("Standard des Telefons", "Phone default"),
        ["Identity.Numbered"] = ("Identität {0}", "Identity {0}"),
        ["Identity.Remove"] = ("Entfernen", "Remove"),
        ["Identity.Manual"] = ("Manuell hinzufügen", "Add manually"),
        ["Identity.ManualHint"] = ("SIP-Adresse der Identität, z. B. 1234@pbx.firma.de", "SIP address of the identity, e.g. 1234@pbx.example.com"),
        ["Identity.ShowSelectionHelp"] = (
            "Zeigt im Wählfenster eine Auswahl, über welche Leitung gewählt werden soll.",
            "Shows a line selection in the dial window."),
        ["Identity.Add"] = ("Hinzufügen", "Add"),
        ["Identity.ShowSelection"] = ("Identität im Wählfenster auswählbar machen", "Allow choosing the identity in the dial window"),

        // ---------- Dialing page ----------
        ["Dialing.Title"] = ("Wählen", "Dialing"),
        ["Dialing.ConfirmLinks"] = ("Vor dem Wählen von Links nachfragen", "Ask before dialing links"),
        ["Dialing.ConfirmLinksHint"] = (
            "Beim Klick auf einen tel:-Link erscheint ein Fenster mit der Nummer – Enter wählt, Esc bricht ab. Ohne Nachfrage wird sofort gewählt.",
            "Clicking a tel: link shows a window with the number – Enter dials, Esc cancels. Without confirmation the number is dialed right away."),
        ["Dialing.ShowNotification"] = ("Benachrichtigung mit Auflegen-Taste anzeigen für", "Show notification with hang-up button for"),
        ["Dialing.Seconds"] = ("Sekunden", "seconds"),
        ["Dialing.NormalizeTitle"] = ("Rufnummern aufbereiten", "Number formatting"),
        ["Dialing.NormalizeIntro"] = (
            "Leerzeichen, Klammern, Schrägstriche und Bindestriche werden immer entfernt.",
            "Spaces, parentheses, slashes and dashes are always removed."),
        ["Dialing.ConvertOwnCountry"] = ("Eigene Landesvorwahl durch 0 ersetzen", "Replace own country code with 0"),
        ["Dialing.PlusToZeros"] = ("Bei ausländischen Nummern + durch 00 ersetzen", "Replace + with 00 for foreign numbers"),
        ["Dialing.ConvertOwnCountryHelp"] = (
            "Aus +49 30 1234 bzw. 0049 30 1234 wird 030 1234. Für Österreich 43, für die Schweiz 41 eintragen.",
            "Turns +49 30 1234 or 0049 30 1234 into 030 1234. Use 43 for Austria, 41 for Switzerland, 44 for the UK."),
        ["Dialing.PlusToZerosHelp"] = (
            "Aus +43 1 2345 wird 0043 1 2345. Manche Telefonanlagen können kein + wählen.",
            "Turns +43 1 2345 into 0043 1 2345. Some phone systems cannot dial a +."),
        ["Dialing.OutsideLine"] = ("Amtsholung", "Outside line prefix"),
        ["Dialing.OutsideLineFrom"] = ("bei Nummern ab", "for numbers with at least"),
        ["Dialing.Digits"] = ("Ziffern", "digits"),
        ["Dialing.OutsideLineHint"] = (
            "Leer lassen, wenn keine Amtsholung nötig ist. Kürzere Nummern (interne Durchwahlen) werden ohne Amtsholung gewählt.",
            "Leave empty if no prefix is needed. Shorter numbers (internal extensions) are dialed without it."),
        ["Dialing.TryIt"] = ("Ausprobieren", "Try it"),

        // ---------- Hotkey page ----------
        ["Hotkey.Title"] = ("Hotkey", "Hotkey"),
        ["Hotkey.Intro"] = (
            "Markiere in einem beliebigen Programm eine Telefonnummer und drücke den Hotkey – das Wählfenster öffnet sich mit der Nummer, Enter wählt. Ist nichts markiert, öffnet sich ein leeres Wählfenster.",
            "Select a phone number in any application and press the hotkey – the dial window opens with the number, Enter dials. If nothing is selected, an empty dial window opens."),
        ["Hotkey.Enabled"] = ("Hotkey aktivieren", "Enable hotkey"),
        ["Hotkey.Combination"] = ("Tastenkombination", "Key combination"),
        ["Hotkey.BoxHint"] = ("Hier klicken und die gewünschte Tastenkombination drücken", "Click here and press the desired key combination"),
        ["Hotkey.Reset"] = ("Standard", "Default"),
        ["Hotkey.Recording"] = ("Drücke jetzt die gewünschte Tastenkombination …", "Now press the desired key combination …"),
        ["Hotkey.Available"] = ("Tastenkombination ist frei", "Key combination is available"),
        ["Hotkey.InUse"] = ("Wird bereits von einem anderen Programm verwendet", "Already used by another application"),
        ["Hotkey.NeedModifier"] = ("Bitte mit Strg, Alt, Umschalt oder Win kombinieren", "Please combine with Ctrl, Alt, Shift or Win"),
        ["Hotkey.ClipboardFallback"] = (
            "Wenn nichts markiert ist: Nummer aus der Zwischenablage übernehmen",
            "If nothing is selected: use a number from the clipboard"),
        ["Hotkey.ClipboardFallbackHelp"] = (
            "Praktisch, wenn du Nummern lieber mit Strg+C kopierst: Steht eine Telefonnummer in der Zwischenablage, wird sie ins Wählfenster übernommen.",
            "Handy if you prefer copying numbers with Ctrl+C: if the clipboard contains a phone number, it is put into the dial window."),
        ["Hotkey.InUseTitle"] = ("Hotkey nicht verfügbar", "Hotkey not available"),
        ["Hotkey.InUseText"] = ("{0} wird bereits von einem anderen Programm verwendet. Bitte wähle eine andere Tastenkombination.", "{0} is already used by another application. Please choose a different combination."),

        // ---------- Protocols page ----------
        ["Protocols.Title"] = ("Links", "Links"),
        ["Protocols.Intro"] = (
            "Welche Link-Arten soll die App öffnen? tel: ist der Standard für Telefonnummern auf Webseiten und in vielen Programmen.",
            "Which link types should the app open? tel: is the standard for phone numbers on websites and in many applications."),
        ["Protocols.Tel"] = ("tel: – Telefonnummern auf Webseiten, in E-Mails, CRM …", "tel: – phone numbers on websites, in e-mails, CRM …"),
        ["Protocols.Callto"] = ("callto: – ältere Variante, z. B. in manchen CRM-Systemen", "callto: – older variant, e.g. in some CRM systems"),
        ["Protocols.Sip"] = ("sip: – SIP-Adressen", "sip: – SIP addresses"),
        ["Protocols.DefaultTitle"] = ("Als Standard-App festlegen", "Set as default app"),
        ["Protocols.DefaultHint"] = (
            "Windows erlaubt Programmen nicht, sich selbst als Standard festzulegen. Klicke auf den Button und wähle in den Windows-Einstellungen bei TEL (und ggf. CALLTO und SIP) „Click to Call for Snom“ aus.",
            "Windows does not allow apps to make themselves the default. Click the button and pick \"Click to Call for Snom\" for TEL (and CALLTO / SIP if desired) in the Windows settings."),
        ["Protocols.OpenDefaultApps"] = ("Windows-Standard-Apps öffnen", "Open Windows default apps"),
        ["Protocols.Refresh"] = ("Status aktualisieren", "Refresh status"),
        ["Protocols.IsDefault"] = ("Standard-App", "Default app"),
        ["Protocols.NotDefault"] = ("Nicht Standard", "Not default"),

        // ---------- General page ----------
        ["General.Title"] = ("Allgemein", "General"),
        ["General.Language"] = ("Sprache", "Language"),
        ["General.LanguageSystem"] = ("Wie Windows", "Same as Windows"),
        ["General.Autostart"] = ("Mit Windows starten", "Start with Windows"),
        ["General.AutostartHelp"] = (
            "Damit tel:-Links und der Hotkey nach jedem Neustart sofort funktionieren. Die App startet unsichtbar im Infobereich.",
            "So that tel: links and the hotkey work right after every restart. The app starts hidden in the notification area."),
        ["General.Installed"] = ("Installiert: Version {0}", "Installed: version {0}"),
        ["General.UpdatesTitle"] = ("Updates", "Updates"),
        ["General.CheckUpdates"] = ("Automatisch nach neuen Versionen suchen", "Automatically check for new versions"),
        ["General.CheckNow"] = ("Jetzt suchen", "Check now"),
        ["General.Tools"] = ("Werkzeuge", "Tools"),
        ["General.OpenLog"] = ("Protokoll öffnen", "Open log"),
        ["General.OpenFolder"] = ("Einstellungsordner öffnen", "Open settings folder"),

        // ---------- Updates ----------
        ["Update.Checking"] = ("Suche …", "Checking …"),
        ["Update.UpToDate"] = ("Du verwendest die neueste Version", "You are using the latest version"),
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
        ["Nav.Phone"] = ("Telefon", "Phone"),
        ["Nav.Dialing"] = ("Wählen", "Dialing"),
        ["Nav.Hotkey"] = ("Hotkey", "Hotkey"),
        ["Nav.Links"] = ("Links", "Links"),
        ["Nav.General"] = ("Allgemein", "General"),

        // ---------- Home ----------
        ["Home.Title"] = ("Start", "Home"),
        ["Home.NoPhone"] = ("Kein Telefon verbunden", "No phone connected"),
        ["Home.NoPhoneText"] = ("Verbinde dein Snom-Telefon, um loszulegen.", "Connect your Snom phone to get started."),
        ["Home.Connect"] = ("Telefon verbinden", "Connect phone"),
        ["Home.DoConnect"] = ("Verbinden", "Connect"),
        ["Home.Change"] = ("Ändern", "Change"),
        ["Home.Recheck"] = ("Verbindung erneut prüfen", "Check connection again"),
        ["Home.Online"] = ("Verbunden", "Connected"),
        ["Home.Offline"] = ("Nicht erreichbar", "Not reachable"),
        ["Home.Checking"] = ("Prüfe Verbindung …", "Checking connection …"),
        ["Home.HostRequired"] = ("Bitte die IP-Adresse des Telefons eingeben.", "Please enter the phone's IP address."),
        ["Home.Scan"] = ("Im Netzwerk suchen", "Search network"),
        ["Home.Scanning"] = ("Suche Snom-Telefone im Netzwerk …", "Searching for Snom phones on the network …"),
        ["Home.ScanFound"] = ("Gefunden – klicke auf dein Telefon:", "Found – click your phone:"),
        ["Home.ScanNone"] = (
            "Keine Snom-Telefone gefunden. Die IP-Adresse steht am Telefon unter Menü → Informationen.",
            "No Snom phones found. You can find the IP address on the phone under Menu → Information."),
        ["Home.NeedsLogin"] = (
            "Dein Telefon ist passwortgeschützt. Bitte die Zugangsdaten der Web-Oberfläche eingeben.",
            "Your phone is password protected. Please enter the web interface credentials."),
        ["Home.Line"] = ("Leitung", "Line"),
        ["Home.LineHelp"] = (
            "Dein Telefon hat mehrere Identitäten (Leitungen). Hier legst du fest, über welche standardmäßig gewählt wird.",
            "Your phone has several identities (lines). Choose which one is used for dialing by default."),
        ["Home.QuickTitle"] = ("Schnellzugriff", "Quick access"),
        ["Home.LinksTitle"] = ("tel:-Links", "tel: links"),
        ["Home.LinksActive"] = (
            "Klicks auf Telefonnummern im Browser und in anderen Programmen wählen auf deinem Telefon.",
            "Clicking phone numbers in the browser and other apps dials on your phone."),
        ["Home.LinksInactive"] = ("Noch nicht als Standard-App für Telefonnummern festgelegt.", "Not yet set as the default app for phone numbers."),
        ["Home.LinksSet"] = ("Als Standard festlegen", "Set as default"),
        ["Home.Active"] = ("✓ Aktiv", "✓ Active"),
        ["Home.HotkeyTitle"] = ("Hotkey", "Hotkey"),
        ["Home.HotkeyActive"] = ("Nummer irgendwo markieren und {0} drücken.", "Select a number anywhere and press {0}."),
        ["Home.HotkeyOff"] = ("Ausgeschaltet", "Turned off"),
        ["Home.TestTitle"] = ("Testanruf", "Test call"),
        ["Home.TestDescription"] = ("Prüfe, ob das Telefon wählt – z. B. mit deiner Handynummer.", "Check whether the phone dials – e.g. with your mobile number."),
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
            "Die App bleibt im Infobereich der Taskleiste aktiv. Links und Hotkey funktionieren weiter.",
            "The app stays active in the notification area. Links and the hotkey keep working."),
    };
}

/// <summary>XAML markup extension: <c>{loc:Tr Some.Key}</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
