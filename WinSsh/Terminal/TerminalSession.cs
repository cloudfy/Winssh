using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using static WinSsh.Terminal.ConPtyNative;

namespace WinSsh.Terminal;

public sealed class TerminalSession : IAsyncDisposable
{
    public event Action<string>? OutputText;

    // Used for host key prompts, passphrase prompts, etc.
    public event Func<string, Task<string?>>? InteractivePrompt;

    private SafePseudoConsoleHandle? _pty;
    private SafeFileHandle? _ptyInputWrite;   // write -> PTY stdin
    private SafeFileHandle? _ptyOutputRead;   // read  <- PTY stdout

    private FileStream? _inputStream;
    private FileStream? _outputStream;

    private PROCESS_INFORMATION _pi;
    private IntPtr _attrList = IntPtr.Zero;

    private CancellationTokenSource? _cts;
    private Task? _pumpTask;

    private readonly Decoder _utf8 = Encoding.UTF8.GetDecoder();
    private string _recent = "";

    public async Task StartSshAsync(string commandLine, int cols = 120, int rows = 30, CancellationToken ct = default)
    {
        if (_pty != null) throw new InvalidOperationException("Session already started.");

        // ConPTY pipes
        ThrowIfFailed(CreatePipe(out var ptyInputRead, out var ptyInputWrite, IntPtr.Zero, 0), "CreatePipe input failed.");
        ThrowIfFailed(CreatePipe(out var ptyOutputRead, out var ptyOutputWrite, IntPtr.Zero, 0), "CreatePipe output failed.");

        // non-child ends not inheritable
        SetHandleInformation(ptyInputWrite, HANDLE_FLAG_INHERIT, 0);
        SetHandleInformation(ptyOutputRead, HANDLE_FLAG_INHERIT, 0);

        _ptyInputWrite = ptyInputWrite;
        _ptyOutputRead = ptyOutputRead;

        // Create pseudo console
        var size = new COORD((short)cols, (short)rows);
        var hr = CreatePseudoConsole(size, ptyInputRead, ptyOutputWrite, 0, out var pty);
        if (hr != 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePseudoConsole failed.");
        _pty = pty;

        // dispose child ends in parent
        ptyInputRead.Dispose();
        ptyOutputWrite.Dispose();

        // attribute list for pseudo console
        var lpSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
        _attrList = Marshal.AllocHGlobal(lpSize);

        ThrowIfFailed(InitializeProcThreadAttributeList(_attrList, 1, 0, ref lpSize), "InitializeProcThreadAttributeList failed.");

        var ptyHandle = _pty.DangerousGetHandle();
        var attr = (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE;

        ThrowIfFailed(UpdateProcThreadAttribute(
            _attrList, 0, attr, ptyHandle, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero), "UpdateProcThreadAttribute failed.");

        var siEx = new STARTUPINFOEX
        {
            StartupInfo = new STARTUPINFO
            {
                cb = Marshal.SizeOf<STARTUPINFOEX>(),
                dwFlags = (int)STARTF_USESTDHANDLES
            },
            lpAttributeList = _attrList
        };

        // Start ssh.exe attached to PTY
        var creationFlags = EXTENDED_STARTUPINFO_PRESENT;
        if (!CreateProcessW(
            null,
            commandLine,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            creationFlags,
            IntPtr.Zero,
            null,
            ref siEx,
            out _pi))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcessW failed.");
        }

        _inputStream = new FileStream(_ptyInputWrite, FileAccess.Write, 4096, isAsync: false);
        _outputStream = new FileStream(_ptyOutputRead, FileAccess.Read, 4096, isAsync: false);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _pumpTask = Task.Run(() => PumpOutputAsync(_cts.Token), _cts.Token);

        OutputText?.Invoke($"Connected: {commandLine}\r\n");
        await Task.CompletedTask;
    }

    public async Task WriteInputAsync(string text, CancellationToken ct = default)
    {
        if (_inputStream == null) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        await _inputStream.WriteAsync(bytes, 0, bytes.Length, ct);
        await _inputStream.FlushAsync(ct);
    }

    public void Resize(int cols, int rows)
    {
        if (_pty == null) return;
        ResizePseudoConsole(_pty, new COORD((short)cols, (short)rows));
    }

    public async Task CloseAsync()
    {
        try { _cts?.Cancel(); } catch { }

        if (_pumpTask != null)
        {
            try { await _pumpTask; } catch { }
        }

        // Close process handles (best effort)
        try
        {
            if (_pi.hThread != IntPtr.Zero) CloseHandle(_pi.hThread);
            if (_pi.hProcess != IntPtr.Zero) CloseHandle(_pi.hProcess);
        }
        catch { }

        _outputStream?.Dispose();
        _inputStream?.Dispose();

        _ptyOutputRead?.Dispose();
        _ptyInputWrite?.Dispose();

        if (_attrList != IntPtr.Zero)
        {
            DeleteProcThreadAttributeList(_attrList);
            Marshal.FreeHGlobal(_attrList);
            _attrList = IntPtr.Zero;
        }

        _pty?.Dispose();
        _pty = null;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _cts?.Dispose();
    }

    private async Task PumpOutputAsync(CancellationToken ct)
    {
        if (_outputStream == null) return;

        var buffer = new byte[4096];
        var charBuf = new char[8192];

        while (!ct.IsCancellationRequested)
        {
            int read;
            try
            {
                read = await _outputStream.ReadAsync(buffer, 0, buffer.Length, ct);
            }
            catch { break; }

            if (read <= 0) break;

            var charCount = _utf8.GetChars(buffer, 0, read, charBuf, 0, flush: false);
            if (charCount <= 0) continue;

            var chunk = new string(charBuf, 0, charCount);
            OutputText?.Invoke(chunk);

            // Prompt detection (OpenSSH)
            _recent = (_recent + chunk);
            if (_recent.Length > 2500) _recent = _recent[^2500..];

            // Host key confirm
            if (_recent.Contains("Are you sure you want to continue connecting", StringComparison.OrdinalIgnoreCase))
            {
                if (InteractivePrompt != null)
                {
                    var answer = await InteractivePrompt.Invoke(
                        "SSH wants to trust this host key.\n\nType: yes (accept) or no (reject).");
                    if (!string.IsNullOrWhiteSpace(answer))
                        await WriteInputAsync(answer.Trim() + "\r", ct);
                }
                _recent = "";
            }
            // Key passphrase
            else if (_recent.Contains("Enter passphrase for key", StringComparison.OrdinalIgnoreCase))
            {
                if (InteractivePrompt != null)
                {
                    var answer = await InteractivePrompt.Invoke("SSH key passphrase required.");
                    if (answer != null)
                        await WriteInputAsync(answer + "\r", ct);
                }
                _recent = "";
            }
            // Password prompt
            else if (_recent.TrimEnd().EndsWith("password:", StringComparison.OrdinalIgnoreCase))
            {
                if (InteractivePrompt != null)
                {
                    var answer = await InteractivePrompt.Invoke("SSH password required.");
                    if (answer != null)
                        await WriteInputAsync(answer + "\r", ct);
                }
                _recent = "";
            }
        }
    }
}