using System.IO;
using System.Runtime.CompilerServices;
using ClickToCall.Core;

namespace ClickToCall.Tests;

internal static class TestSetup
{
    /// <summary>Keeps the tests out of the real settings and log in %APPDATA%.</summary>
    [ModuleInitializer]
    internal static void UseTemporaryDirectory() =>
        SettingsStore.UseDirectory(Path.Combine(Path.GetTempPath(), "ClickToCall.Tests"));
}
