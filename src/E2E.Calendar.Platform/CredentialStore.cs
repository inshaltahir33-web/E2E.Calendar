using System.ComponentModel;
using System.Runtime.InteropServices;
using E2E.Calendar.Core.Abstractions;

namespace E2E.Calendar.Platform;

public sealed class CredentialStore : ISecretStore
{
    private const string TargetPrefix = "E2E.Calendar:";

    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int ErrorNoSuch = 1168;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Task.FromResult(Read(TargetPrefix + key));
    }

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        Write(TargetPrefix + key, value);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!CredDelete(TargetPrefix + key, CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();

            if (error is not (ErrorNotFound or ErrorNoSuch))
            {
                throw new Win32Exception(error);
            }
        }

        return Task.CompletedTask;
    }

    private static string? Read(string target)
    {
        if (!CredRead(target, CredTypeGeneric, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();

            if (error is ErrorNotFound or ErrorNoSuch)
            {
                return null;
            }

            throw new Win32Exception(error);
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);

            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return null;
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);

            return System.Text.Encoding.Unicode.GetString(bytes);
        }
        finally
        {
            CredFree(pointer);
        }
    }

    private static void Write(string target, string value)
    {
        var bytes = System.Text.Encoding.Unicode.GetBytes(value);
        var blob = Marshal.AllocCoTaskMem(Math.Max(bytes.Length, 1));

        try
        {
            if (bytes.Length > 0)
            {
                Marshal.Copy(bytes, 0, blob, bytes.Length);
            }

            var targetPointer = IntPtr.Zero;
            var userPointer = IntPtr.Zero;

            try
            {
                targetPointer = Marshal.StringToHGlobalUni(target);
                userPointer = Marshal.StringToHGlobalUni(Environment.UserName);

                var credential = new NativeCredential
                {
                    Type = CredTypeGeneric,
                    TargetName = targetPointer,
                    CredentialBlob = blob,
                    CredentialBlobSize = (uint)bytes.Length,
                    Persist = CredPersistLocalMachine,
                    UserName = userPointer,
                };

                if (!CredWrite(ref credential, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                if (targetPointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(targetPointer);
                }

                if (userPointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(userPointer);
                }
            }
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(blob);
        }
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
