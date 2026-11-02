using System.Net;

namespace ClickToCall.Core.Phones;

/// <summary>What the scan just did, for showing it live.</summary>
/// <param name="Fraction">Share of the addresses checked so far, 0 to 1.</param>
/// <param name="Network">The network being scanned, e.g. "192.168.178.x".</param>
/// <param name="Device">Set once for every address with a web server (phone or not).</param>
/// <param name="Phone">Set once for every phone found.</param>
public sealed record ScanUpdate(double Fraction, string Network, IPAddress? Device = null, FoundPhone? Phone = null);
