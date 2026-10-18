<div align="center">

<img src="docs/logo.png" width="96" alt="">

# Click to Call

Click a phone number on your PC – your Snom or Yealink desk phone dials it.

[![Version](https://img.shields.io/github/v/release/julianfaatz/click-to-call?label=Version)](https://github.com/julianfaatz/click-to-call/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/julianfaatz/click-to-call/total?label=Downloads)](https://github.com/julianfaatz/click-to-call/releases)

[Deutsch](README.md) · **English**

</div>

Click to Call dials numbers on your desk phone from a Windows PC – by clicking `tel:` links or by pressing a hotkey on a selected number in any application. The app controls the phone directly through its web interface on the local network; no PBX software or server is needed.

The screenshots show the German UI; the app is also available in English.

![Start page](docs/start.png)

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

1. Download and run `ClickToCall-x.y.z-setup.exe` from [Releases](https://github.com/julianfaatz/click-to-call/releases/latest) (Windows 10/11, no admin rights required). A portable ZIP is available as well.
2. The app looks for the phone on the network. Click the phone it finds or enter its IP address. You need the login of the phone's web interface.
3. Follow the checklist on the start page. In the Windows settings, pick “Click to Call” for **TEL** once – Windows does not let apps do this themselves.

The installer is not code-signed yet. If Windows SmartScreen shows a warning: *More info → Run anyway*. Every release includes SHA-256 checksums.

<table>
  <tr>
    <td><img src="docs/waehlen1.png" alt="Settings: dialing and hotkey"></td>
    <td><img src="docs/waehlen2.png" alt="Settings: links and phone numbers"></td>
  </tr>
</table>

## Dialing from other programs

Other software or scripts can call the app like a link:

```powershell
ClickToCall.exe "tel:+49301234567"
```

## Troubleshooting

| Problem | Solution |
|---|---|
| Phone not found | Enter the IP address manually (phone menu → Information). If the phone is on a different network, it must be reachable from the PC. |
| “Wrong username or password” | Use the login of the phone's web interface, not the SIP credentials. |
| “The phone denied access” | Check in the web interface that the user has admin rights and HTTP control is allowed. |
| Yealink does not dial | Confirm “Allow remote control” on the phone at the first call, or add the PC's IP under *Features → Remote Control* → **Action URI Allow IP List**. |
| Clicking a tel: link does nothing | Enable *Dialing → Links* and pick “Click to Call” in the Windows settings. |
| Hotkey does not work | Choose a different combination under *Dialing → Hotkey*. |

Settings and the log are in `%APPDATA%\ClickToCall`. Still stuck? Use *General → Report a problem* in the app or open an [issue](https://github.com/julianfaatz/click-to-call/issues/new/choose).

## Privacy

The app only talks to your phone and to GitHub to check for updates (can be turned off). There is no telemetry. The phone password is stored encrypted with Windows DPAPI.

## Contributing

How to build, test without a phone and create releases is described in [CONTRIBUTING.md](CONTRIBUTING.md) (German). Changes between versions are listed in the [changelog](CHANGELOG.md).

## License

[MIT](LICENSE) © Julian Faatz

Not an official product of Snom or Yealink. Snom and Yealink are trademarks of their respective owners.
