using System.Runtime.InteropServices;

namespace Wintangle.App.Interop;

/// <summary>Process/token elevation query APIs (kernel32.dll, advapi32.dll).</summary>
internal static class ElevationApi
{
    /// <summary>PROCESS_QUERY_LIMITED_INFORMATION.</summary>
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    /// <summary>TOKEN_QUERY.</summary>
    public const uint TOKEN_QUERY = 0x0008;

    /// <summary>TokenElevation (TOKEN_INFORMATION_CLASS).</summary>
    public const int TokenElevation = 20;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetTokenInformation(
        IntPtr TokenHandle,
        int TokenInformationClass,
        out TOKEN_ELEVATION TokenInformation,
        uint TokenInformationLength,
        out uint ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// True when the current process is running elevated (TokenElevation on
    /// its own token). Never throws; returns false if the query fails.
    /// </summary>
    internal static bool IsProcessElevated()
    {
        var hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)Environment.ProcessId);
        if (hProcess == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (!OpenProcessToken(hProcess, TOKEN_QUERY, out var hToken) || hToken == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return GetTokenInformation(
                        hToken,
                        TokenElevation,
                        out var elevation,
                        (uint)Marshal.SizeOf<TOKEN_ELEVATION>(),
                        out _)
                    && elevation.TokenIsElevated != 0;
            }
            finally
            {
                CloseHandle(hToken);
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }
}
