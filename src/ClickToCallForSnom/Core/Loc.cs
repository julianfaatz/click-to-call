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
        ["Phone.Host"] = ("IP-Adresse / Hostname", "IP address / host name"),
        ["Phone.Connection"] = ("Verbindung", "Connection"),
        ["Phone.UseHttps"] = ("HTTPS verwenden", "Use HTTPS"),
        ["Phone.IgnoreCert"] = ("Zertifikat nicht prüfen (selbstsigniert)", "Don't verify certificate (self-signed)"),
        ["Phone.Username"] = ("Benutzername", "Username"),
        ["Phone.Password"] = ("Passwort", "Password"),
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
        ["General.LanguageHint"] = ("Wird für neu geöffnete Fenster übernommen.", "Applies to newly opened windows."),
        ["General.Autostart"] = ("Mit Windows starten", "Start with Windows"),
        ["General.UpdatesTitle"] = ("Updates", "Updates"),
        ["General.CheckUpdates"] = ("Automatisch nach neuen Versionen suchen", "Automatically check for new versions"),
        ["General.CheckNow"] = ("Jetzt suchen", "Check now"),
        ["General.Tools"] = ("Werkzeuge", "Tools"),
        ["General.RunWizard"] = ("Einrichtungsassistent starten", "Run setup assistant"),
        ["General.OpenLog"] = ("Protokoll öffnen", "Open log"),
        ["General.OpenFolder"] = ("Einstellungsordner öffnen", "Open settings folder"),
        ["General.About"] = ("Info", "About"),

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

        // ---------- Windows ----------
        ["Settings.Title"] = ("Click to Call for Snom – Einstellungen", "Click to Call for Snom – Settings"),
        ["Tab.Phone"] = ("Telefon", "Phone"),
        ["Tab.Dialing"] = ("Wählen", "Dialing"),
        ["Tab.Hotkey"] = ("Hotkey", "Hotkey"),
        ["Tab.Protocols"] = ("Links", "Links"),
        ["Tab.General"] = ("Allgemein", "General"),

        // ---------- Wizard ----------
        ["Wizard.Title"] = ("Click to Call for Snom – Einrichtung", "Click to Call for Snom – Setup"),
        ["Wizard.Back"] = ("Zurück", "Back"),
        ["Wizard.Next"] = ("Weiter", "Next"),
        ["Wizard.Finish"] = ("Fertigstellen", "Finish"),
        ["Wizard.Step"] = ("Schritt {0} von {1}", "Step {0} of {1}"),
        ["Wizard.WelcomeTitle"] = ("Willkommen bei Click to Call for Snom", "Welcome to Click to Call for Snom"),
        ["Wizard.WelcomeText"] = (
            "Mit dieser App wählst du Telefonnummern direkt auf deinem Snom-Tischtelefon: per Klick auf tel:-Links im Browser oder in anderen Programmen, oder per Hotkey für markierte Nummern. In den nächsten Schritten richten wir alles gemeinsam ein.",
            "This app dials phone numbers directly on your Snom desk phone: by clicking tel: links in the browser or other applications, or with a hotkey for selected numbers. The next steps will guide you through the setup."),
        ["Wizard.HostRequired"] = ("Bitte gib die IP-Adresse deines Telefons ein.", "Please enter your phone's IP address."),
        ["Wizard.FinishTitle"] = ("Fast geschafft!", "Almost done!"),
        ["Wizard.FinishText"] = (
            "Die App läuft im Hintergrund und ist über das Symbol im Infobereich der Taskleiste erreichbar (ggf. unter dem Pfeil ^). Dort findest du auch die Einstellungen.",
            "The app runs in the background and is available via the icon in the notification area of the taskbar (possibly under the ^ arrow). The settings are there as well."),
        ["Wizard.TestCallTitle"] = ("Testanruf", "Test call"),
        ["Wizard.TestCallHint"] = ("Gib eine Nummer ein (z. B. dein Handy) und prüfe, ob das Telefon wählt.", "Enter a number (e.g. your mobile) and check whether the phone dials."),
        ["Wizard.TestCall"] = ("Anrufen", "Call"),
        ["Wizard.TestCallOk"] = ("Das Telefon hat den Wählbefehl angenommen.", "The phone accepted the dial command."),
        ["Wizard.TestCallFailed"] = ("Das hat nicht geklappt – Details in der Benachrichtigung.", "That didn't work – see the notification for details."),

        // ---------- Tray ----------
        ["Tray.Dial"] = ("Nummer wählen …", "Dial number …"),
        ["Tray.Redial"] = ("Wahlwiederholung", "Redial"),
        ["Tray.RedialNumber"] = ("Wahlwiederholung: {0}", "Redial: {0}"),
        ["Tray.Settings"] = ("Einstellungen …", "Settings …"),
        ["Tray.Wizard"] = ("Einrichtungsassistent …", "Setup assistant …"),
        ["Tray.CheckUpdates"] = ("Nach Updates suchen", "Check for updates"),
        ["Tray.Exit"] = ("Beenden", "Exit"),

        // ---------- Ready ----------
        ["Ready.Title"] = ("Click to Call for Snom ist bereit", "Click to Call for Snom is ready"),
        ["Ready.Text"] = ("Die App läuft im Infobereich der Taskleiste.", "The app is running in the notification area."),
        ["Ready.TextHotkey"] = ("Die App läuft im Infobereich. Nummer markieren und {0} drücken zum Wählen.", "The app is running in the notification area. Select a number and press {0} to dial."),
    };
}

/// <summary>XAML markup extension: <c>{loc:Tr Some.Key}</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
