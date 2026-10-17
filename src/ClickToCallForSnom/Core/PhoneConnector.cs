namespace ClickToCallForSnom.Core;

public enum ConnectStatus
{
    Connected,
    NeedsCredentials,
    WrongCredentials,
    Failed,
}

public sealed record ConnectResult(ConnectStatus Status, bool UseHttps, string Model, PhoneException? Error);

/// <summary>Finds out how to talk to a phone: tries HTTP and HTTPS and detects whether a login is needed.</summary>
public static class PhoneConnector
{
    public static async Task<ConnectResult> ConnectAsync(string host, string username, string password, bool preferHttps, CancellationToken ct = default)
    {
        PhoneException? lastError = null;
        bool[] schemes = preferHttps ? [true, false] : [false, true];

        foreach (var https in schemes)
        {
            try
            {
                using var client = new SnomPhoneClient(new PhoneConnection(host, https, true, username, password), TimeSpan.FromSeconds(4));
                var model = await client.TestConnectionAsync(ct);
                return new ConnectResult(ConnectStatus.Connected, https, model, null);
            }
            catch (PhoneException ex) when (ex.Kind == PhoneErrorKind.Unauthorized)
            {
                var status = username.Length == 0 && password.Length == 0 ? ConnectStatus.NeedsCredentials : ConnectStatus.WrongCredentials;
                return new ConnectResult(status, https, "", ex);
            }
            catch (PhoneException ex) when (ex.Kind is PhoneErrorKind.Unreachable or PhoneErrorKind.Timeout or PhoneErrorKind.Certificate)
            {
                lastError = ex; // try the other scheme
            }
            catch (PhoneException ex)
            {
                return new ConnectResult(ConnectStatus.Failed, https, "", ex); // the phone answered, but with an error
            }
        }

        return new ConnectResult(ConnectStatus.Failed, false, "", lastError);
    }
}
