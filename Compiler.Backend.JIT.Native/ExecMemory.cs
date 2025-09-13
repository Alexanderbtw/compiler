using System.Runtime.InteropServices;

namespace Compiler.Backend.JIT.Native;

internal sealed class ExecMemory : IDisposable
{
    private readonly nuint _size;

    public ExecMemory(
        nuint size)
    {
        _size = size;
        Pointer = Allocate(size);

        if (Pointer == IntPtr.Zero)
        {
            throw new InvalidOperationException("mmap/VirtualAlloc failed");
        }
    }

    public IntPtr Pointer { get; private set; }

    public void Dispose()
    {
        if (Pointer != IntPtr.Zero)
        {
            Free(
                ptr: Pointer,
                size: _size);

            Pointer = IntPtr.Zero;
        }
    }

    public void Write(
        ReadOnlySpan<byte> code)
    {
        if ((nuint)code.Length > _size)
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }

        Marshal.Copy(
            source: code.ToArray(),
            startIndex: 0,
            destination: Pointer,
            length: code.Length);

        if (!ProtectRX(
                ptr: Pointer,
                size: _size))
        {
            throw new InvalidOperationException("mprotect/VirtualProtect failed");
        }
    }

    private static IntPtr Allocate(
        nuint size)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            const uint MEM_COMMIT = 0x1000;
            const uint MEM_RESERVE = 0x2000;
            const uint PAGE_READWRITE = 0x04;

            return VirtualAlloc(
                lpAddress: IntPtr.Zero,
                dwSize: new UIntPtr(size),
                flAllocationType: MEM_COMMIT | MEM_RESERVE,
                flProtect: PAGE_READWRITE);
        }

        const int PROT_READ = 1;
        const int PROT_WRITE = 2;
        const int MAP_PRIVATE = 2;
        const int MAP_ANON = 0x1000;
        IntPtr res = mmap(
            addr: IntPtr.Zero,
            length: size,
            prot: PROT_READ | PROT_WRITE,
            flags: MAP_PRIVATE | MAP_ANON,
            fd: -1,
            offset: IntPtr.Zero);

        return res.ToInt64() == -1
            ? IntPtr.Zero
            : res;
    }

    private static void Free(
        IntPtr ptr,
        nuint size)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            const uint MEM_RELEASE = 0x8000;
            VirtualFree(
                lpAddress: ptr,
                dwSize: UIntPtr.Zero,
                dwFreeType: MEM_RELEASE);
        }
        else
        {
            munmap(
                addr: ptr,
                length: size);
        }
    }

    [DllImport(
        "libc",
        SetLastError = true)]
    private static extern IntPtr mmap(
        IntPtr addr,
        nuint length,
        int prot,
        int flags,
        int fd,
        IntPtr offset);

    [DllImport(
        "libc",
        SetLastError = true)]
    private static extern int mprotect(
        IntPtr addr,
        nuint len,
        int prot);

    [DllImport(
        "libc",
        SetLastError = true)]
    private static extern int munmap(
        IntPtr addr,
        nuint length);

    private static bool ProtectRX(
        IntPtr ptr,
        nuint size)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            const uint PAGE_EXECUTE_READ = 0x20;

            return VirtualProtect(
                lpAddress: ptr,
                dwSize: new UIntPtr(size),
                flNewProtect: PAGE_EXECUTE_READ,
                lpflOldProtect: out _);
        }

        const int PROT_READ = 1;
        const int PROT_EXEC = 4;

        return mprotect(
            addr: ptr,
            len: size,
            prot: PROT_READ | PROT_EXEC) == 0;
    }

    [DllImport(
        "kernel32",
        SetLastError = true)]
    private static extern IntPtr VirtualAlloc(
        IntPtr lpAddress,
        UIntPtr dwSize,
        uint flAllocationType,
        uint flProtect);

    [DllImport(
        "kernel32",
        SetLastError = true)]
    private static extern bool VirtualFree(
        IntPtr lpAddress,
        UIntPtr dwSize,
        uint dwFreeType);

    [DllImport(
        "kernel32",
        SetLastError = true)]
    private static extern bool VirtualProtect(
        IntPtr lpAddress,
        UIntPtr dwSize,
        uint flNewProtect,
        out uint lpflOldProtect);
}
