# Copyright (c) Zenix. All rights reserved.
# Licensed under the proprietary and confidential license.

"""
Desktop Bridge Runner for agent-desktop on Windows.
Bridges agent-desktop commands from isolated agent sandbox desktops to the interactive user desktop (WinSta0\\default).
"""

import sys
import io
import os
import ctypes
import subprocess
from ctypes import wintypes

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')

class STARTUPINFOW(ctypes.Structure):
    _fields_ = [
        ('cb', wintypes.DWORD), ('lpReserved', wintypes.LPWSTR), ('lpDesktop', wintypes.LPWSTR),
        ('lpTitle', wintypes.LPWSTR), ('dwX', wintypes.DWORD), ('dwY', wintypes.DWORD),
        ('dwXSize', wintypes.DWORD), ('dwYSize', wintypes.DWORD), ('dwXCountChars', wintypes.DWORD),
        ('dwYCountChars', wintypes.DWORD), ('dwFillAttribute', wintypes.DWORD), ('dwFlags', wintypes.DWORD),
        ('wShowWindow', wintypes.WORD), ('cbReserved2', wintypes.WORD), ('lpReserved2', ctypes.c_void_p),
        ('hStdInput', wintypes.HANDLE), ('hStdOutput', wintypes.HANDLE), ('hStdError', wintypes.HANDLE)
    ]

class PROCESS_INFORMATION(ctypes.Structure):
    _fields_ = [
        ('hProcess', wintypes.HANDLE), ('hThread', wintypes.HANDLE),
        ('dwProcessId', wintypes.DWORD), ('dwThreadId', wintypes.DWORD)
    ]

def launch_target_app(exe_path, cwd=None):
    if cwd is None:
        cwd = os.path.dirname(exe_path)

    # Launch persistently via WMI CIM Win32_Process so it is detached from sandbox job objects
    ps_cmd = f"""
    $res = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{{
        CommandLine = '"{exe_path}"'
        CurrentDirectory = '{cwd}'
    }}
    Write-Output "$($res.ReturnValue):$($res.ProcessId)"
    """
    res = subprocess.run(['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', ps_cmd], capture_output=True, text=True)
    out = res.stdout.strip()
    if ':' in out:
        ret_val, pid = out.split(':', 1)
        if ret_val.strip() == '0':
            print(f'{{"ok":true,"command":"launch-app","data":{{"pid":{pid.strip()},"path":"{exe_path}"}}}}')
            return

    # Fallback to direct CreateProcessW
    kernel32 = ctypes.windll.kernel32
    si = STARTUPINFOW()
    si.cb = ctypes.sizeof(STARTUPINFOW)
    si.lpDesktop = r'WinSta0\default'
    pi = PROCESS_INFORMATION()

    ok = kernel32.CreateProcessW(exe_path, None, None, None, False, 0x00000200, None, cwd, ctypes.byref(si), ctypes.byref(pi))
    if not ok:
        err = kernel32.GetLastError()
        print(f'{{"ok":false,"error":{{"code":"LAUNCH_FAILED","message":"CreateProcess on WinSta0\\\\default failed with error {err}"}}}}')
        sys.exit(1)

    print(f'{{"ok":true,"command":"launch-app","data":{{"pid":{pi.dwProcessId},"path":"{exe_path}"}}}}')
    kernel32.CloseHandle(pi.hProcess)
    kernel32.CloseHandle(pi.hThread)

def run_agent_desktop(args):
    npm_path = os.path.expandvars(r'%APPDATA%\npm\node_modules\agent-desktop\bin\agent-desktop-win32-x64.exe')
    if not os.path.exists(npm_path):
        npm_path = 'agent-desktop'

    kernel32 = ctypes.windll.kernel32
    si = STARTUPINFOW()
    si.cb = ctypes.sizeof(STARTUPINFOW)
    si.lpDesktop = r'WinSta0\default'
    pi = PROCESS_INFORMATION()

    quoted_args = []
    for a in args:
        if ' ' in a or '\t' in a or '"' in a:
            quoted_args.append('"' + a.replace('"', '\\"') + '"')
        else:
            quoted_args.append(a)

    cmd_line = f'"{npm_path}" ' + ' '.join(quoted_args)
    temp_dir = os.environ.get('TEMP', r'C:\Users\huynh\AppData\Local\Temp')
    temp_out = os.path.join(temp_dir, f'agent_desktop_bridge_{os.getpid()}.txt')
    full_cmd = f'cmd.exe /c "{cmd_line} > "{temp_out}" 2>&1"'

    ok = kernel32.CreateProcessW(None, full_cmd, None, None, False, 0, None, None, ctypes.byref(si), ctypes.byref(pi))
    if not ok:
        err = kernel32.GetLastError()
        print(f'{{"ok":false,"error":{{"code":"LAUNCH_FAILED","message":"CreateProcess on WinSta0\\\\default failed with error {err}"}}}}')
        sys.exit(1)

    kernel32.WaitForSingleObject(pi.hProcess, 60000)
    kernel32.CloseHandle(pi.hProcess)
    kernel32.CloseHandle(pi.hThread)

    if os.path.exists(temp_out):
        with open(temp_out, 'r', encoding='utf-8', errors='replace') as f:
            content = f.read()
        try:
            os.remove(temp_out)
        except OSError:
            pass
        print(content, end='')
    else:
        print('{"ok":false,"error":{"code":"NO_OUTPUT","message":"No output produced from agent-desktop"}}')

def main():
    if len(sys.argv) > 1 and sys.argv[1] == 'launch-app':
        if len(sys.argv) < 3:
            print('{"ok":false,"error":{"code":"INVALID_ARGS","message":"Usage: bridge.py launch-app <exe_path> [cwd]"}}')
            sys.exit(1)
        exe_path = sys.argv[2]
        cwd = sys.argv[3] if len(sys.argv) > 3 else None
        launch_target_app(exe_path, cwd)
    else:
        run_agent_desktop(sys.argv[1:])

if __name__ == '__main__':
    main()
