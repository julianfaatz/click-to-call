namespace ClickToCallForSnom.Core;

public enum ConnectStatus
{
    Connected,
    NeedsCredentials,
    WrongCredentials,
    /// <summary>Something answers at the address, but the manufacturer could not be recognized.</summary>
    UnknownVendor,
    Failed,
}

public sealed record ConnectResult(ConnectStatus Status, PhoneVendor? Vendor, bool UseHttps, string Model, PhoneException? Error);

/// <summary>
/// Finds out how to talk to a phone: recognizes the manufacturer (unless given), tries HTTP and HTTPS
/// and detects whether a login is needed.
/// </summary>
public static class PhoneConnector
{
    /// <param name="vendor">Null = recognize it from the phone's web page.</param>
    public static async Task<ConnectResult> ConnectAsync(
        PhoneVendor? vendor, string host, string username, string password, bool preferHttps, CancellationToken ct = default)
    {
        host = HttpPhoneClient.NormalizeHost(host);
        if (!Uri.TryCreate($"http://{host}/", UriKind.Absolute, out _))
            return new ConnectResult(ConnectStatus.Failed, vendor, false, "", new PhoneException(PhoneErrorKind.InvalidAddress, host));

        if (vendor is null)
        {
            var (status, phone) = await PhoneScanner.IdentifyAsync(host, ct);
            switch (status)
            {
                case ProbeStatus.Unreachable:
                    return new ConnectResult(ConnectStatus.Failed, null, false, "", new PhoneException(PhoneErrorKind.Unreachable));
                case ProbeStatus.UnknownDevice:
                    return new ConnectResult(ConnectStatus.UnknownVendor, null, false, "", null);
            }

            vendor = phone!.Vendor;
        }

        PhoneException? lastError = null;
        bool[] schemes = preferHttps ? [true, false] : [false, true];

        foreach (var https in schemes)
        {
            try
            {
                using var client = PhoneDrivers.CreateClient(
                    new PhoneConnection(host, https, true, username, password, vendor.Value), TimeSpan.FromSeconds(4));
                var model = await client.TestConnectionAsync(ct);
                return new ConnectResult(ConnectStatus.Connected, vendor, https, model, null);
            }
            catch (PhoneException ex) when (ex.Kind == PhoneErrorKind.Unauthorized)
            {
                var status = username.Length == 0 && password.Length == 0 ? ConnectStatus.NeedsCredentials : ConnectStatus.WrongCredentials;
                return new ConnectResult(status, vendor, https, "", ex);
            }
            catch (PhoneException ex) when (ex.Kind is PhoneErrorKind.Unreachable or PhoneErrorKind.Timeout or PhoneErrorKind.Certificate)
            {
                lastError = ex; // try the other scheme
            }
            catch (PhoneException ex)
            {
                return new ConnectResult(ConnectStatus.Failed, vendor, https, "", ex); // the phone answered, but with an error
            }
        }

        return new ConnectResult(ConnectStatus.Failed, vendor, false, "", lastError);
    }
}
