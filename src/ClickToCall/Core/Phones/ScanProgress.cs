using System.Net;

namespace ClickToCall.Core.Phones;

/// <summary>Counts the checked addresses of a scan and reports them, thread-safe.</summary>
internal sealed class ScanProgress(IProgress<ScanUpdate>? progress, int total)
{
    private int _done;
    private int _lastPercent = -1;

    /// <summary>Only whole percents: <see cref="Progress{T}"/> posts every report to the UI thread.</summary>
    public void AddressChecked(string network)
    {
        var percent = Interlocked.Increment(ref _done) * 100 / total;
        for (var last = Volatile.Read(ref _lastPercent); last < percent; last = Volatile.Read(ref _lastPercent))
        {
            if (Interlocked.CompareExchange(ref _lastPercent, percent, last) == last)
            {
                progress?.Report(new ScanUpdate(percent / 100.0, network));
                break;
            }
        }
    }

    public void DeviceAnswered(string network, IPAddress device, FoundPhone? phone) =>
        progress?.Report(new ScanUpdate(Volatile.Read(ref _done) / (double)total, network, device, phone));
}
