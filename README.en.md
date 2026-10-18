<div align="center">

<img src="docs/logo.png" width="96" alt="">

# Click to Call

Click a phone number on your PC – your Snom or Yealink desk phone dials it.

[![Version](https://img.shields.io/github/v/release/julianfaatz/click-to-call?label=Version)](https://github.com/julianfaatz/click-to-call/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/julianfaatz/click-to-call/total?label=Downloads)](https://github.com/julianfaatz/click-to-call/releases)

[Deutsch](README.md) · **English**

<img src="docs/demo.gif" alt="Clicking a phone number – the phone dials">

</div>

The app controls the phone directly on the local network – no server, no PBX software.

The screenshots show the German UI; the app is also available in English.

## Features

- 📞 `tel:`, `callto:` and `sip:` links open on the phone (browser, Outlook, CRM …)
- ⌨️ Select a number and press <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>W</kbd> – works in PDFs and e-mails, too
- Numbers are normalized: `+49 (0)30 123 456-78` → `03012345678`, optionally with an outside line prefix
- Confirmation before dialing, hang-up button, redial, line selection
- Finds the phone on the network by itself
- Runs in the notification area, one-click updates
- German and English

<img src="docs/hotkey-popup.png" width="400" alt="Dial window after Ctrl+Alt+W">

## Supported phones

| Vendor | Series | Models (examples) |
|---|---|---|
| **Snom** | D3xx, D7xx | D305, D315, D335, D345, D385, D713, D715, D717, D735, D785 |
| **Yealink** | T3x, T4x, T5x | T31, T33, T42, T43, T44, T46, T48, T53, T54, T57 |

The phone's HTTP remote control is required: `command.htm` on Snom, “Action URI” on Yealink. According to Snom, the D8xx series does not support this interface.

## Installation

1. **Download:** run `ClickToCall-x.y.z-setup.exe` from [Releases](https://github.com/julianfaatz/click-to-call/releases/latest). Windows 10/11, no admin rights needed. Also available as a portable ZIP.
2. **Connect the phone:** click the phone the app finds or enter its IP address. Then enter the username and password of the phone's web interface.
3. **Enable links:** in the Windows settings, pick “Click to Call” for **TEL**. The checklist on the start page takes you there.

> **SmartScreen warning?** *More info → Run anyway.* The installer is not code-signed; every release includes SHA-256 checksums.

![Start page after setup](docs/start.png)

<table>
  <tr>
    <td><img src="docs/waehlen1.png" alt="Settings: dialing and hotkey"></td>
    <td><img src="docs/waehlen2.png" alt="Settings: links and phone numbers"></td>
  </tr>
</table>

## Dialing from other programs

Other programs or scripts just call the app with the number:

```powershell
& "$env:LOCALAPPDATA\Programs\Click to Call\ClickToCall.exe" "tel:+49301234567"
```

## Troubleshooting

| Problem | Solution |
|---|---|
| Phone not found | Enter the IP address manually (phone menu → *Information*). |
| “Wrong username or password” | Use the web interface login, not the SIP credentials. |
| “The phone denied access” | The user needs admin rights and HTTP control must be allowed. |
| Yealink does not dial | Confirm “Allow remote control” on the phone at the first call. Or add the PC's IP under *Features → Remote Control* → **Action URI Allow IP List**. |
| Clicking a tel: link does nothing | Enable *Dialing → Links* and pick “Click to Call” for **TEL** in Windows. |
| Hotkey does not work | Choose a different combination under *Dialing → Hotkey*. |

Still stuck? Use *General → Report a problem* in the app or open an [issue](https://github.com/julianfaatz/click-to-call/issues/new/choose). Settings and the log are in `%APPDATA%\ClickToCall`.

## Privacy

- Only talks to your phone and to GitHub (update check, can be turned off)
- No telemetry
- Phone password is stored encrypted (Windows DPAPI)

## Contributing

- Building, testing without a phone, releases: [CONTRIBUTING.md](CONTRIBUTING.md) (German)
- Changes per version: [changelog](CHANGELOG.md)

## License

[MIT](LICENSE) © Julian Faatz

Not an official product of Snom or Yealink. Snom and Yealink are trademarks of their respective owners.
