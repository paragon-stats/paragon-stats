using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ParagonStats.Core.Tui;

/// <summary>
/// Turns on the console's ANSI interpretation, and reports honestly when it
/// cannot.
/// <para>
/// Windows Terminal and ConPTY have virtual-terminal processing on already.
/// Classic conhost - what `cmd.exe` still gets - has it **off by default**, and
/// .NET does not enable it for you. Painting escapes into that prints literal
/// `ESC[2J` garbage, which is a worse failure than the plain text it replaced,
/// so the text UI only runs when this returns true.
/// </para>
/// </summary>
internal static class VirtualTerminal
{
    private const int StdOutputHandle = -11;
    private const int EnableVirtualTerminalProcessing = 0x0004;

    /// <summary>
    /// True when ANSI will be interpreted. Never throws: a host that refuses
    /// every one of these calls just means plain mode, not a crash on startup.
    /// </summary>
    public static bool TryEnable()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Every other terminal this could run on interprets ANSI already.
            return true;
        }

        try
        {
            nint handle = GetStdHandle(StdOutputHandle);
            if (handle == 0 || handle == -1)
            {
                return false;
            }

            if (!GetConsoleMode(handle, out uint mode))
            {
                return false;
            }

            return (mode & EnableVirtualTerminalProcessing) != 0
                || SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    // DllImport rather than the source-generated LibraryImport on purpose:
    // LibraryImport requires AllowUnsafeBlocks for the whole assembly, which is
    // a large posture change to buy three calls with blittable arguments.
    // DllImport is equally AOT-safe here - nothing needs custom marshalling.
#pragma warning disable SYSLIB1054 // see above; AllowUnsafeBlocks is not worth it for this
    [DllImport("kernel32.dll", SetLastError = true)]
    [SupportedOSPlatform("windows")]
    private static extern nint GetStdHandle(int handleKind);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    [SupportedOSPlatform("windows")]
    private static extern bool GetConsoleMode(nint console, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    [SupportedOSPlatform("windows")]
    private static extern bool SetConsoleMode(nint console, uint mode);
#pragma warning restore SYSLIB1054
}
