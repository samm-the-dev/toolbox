# Python Reference

Windows-specific patterns and gotchas for Python development. For core rules, see [AGENTS.md](AGENTS.md).

## Path Containment

`str.startswith()` is bypassable for path security checks:

```python
# Bad - "/foo/bar2".startswith("/foo/bar") is True
if not str(member_path).startswith(str(base_path)):
    raise ValueError("Path traversal detected")

# Good - pathlib-aware containment check
if not member_path.is_relative_to(base_path):
    raise ValueError("Path traversal detected")
```

This matters for zip extraction (Zip Slip), file access controls, and any path-based allowlisting.

## Encoding: PowerShell 5.1 BOM

PowerShell 5.1's `-Encoding UTF8` writes a UTF-8 BOM (byte order mark). Python's default `utf-8` codec doesn't strip it:

```python
# Bad - fails on BOM-prefixed files
with open(path, encoding="utf-8") as f:
    data = json.load(f)  # JSONDecodeError on BOM

# Good - utf-8-sig strips BOM if present, works without BOM too
with open(path, encoding="utf-8-sig") as f:
    data = json.load(f)
```

Use `utf-8-sig` for any file that might be written by PowerShell 5.1 (config files, JSON, CSV). See also [shell-reference.md](shell-reference.md#powershell-encoding-gotcha) for the Node.js equivalent.

## pythonw.exe: No Console

`pythonw.exe` runs under the Windows GUI subsystem -- no console window is created. This means `sys.stdout` and `sys.stderr` are `None`, not file objects:

```python
# Bad - StreamHandler writes to sys.stderr (None under pythonw)
logging.basicConfig(level=logging.INFO)

# Good - guard console handlers
logger = logging.getLogger(__name__)
if sys.stderr is not None:
    console_handler = logging.StreamHandler()
    logger.addHandler(console_handler)
```

Also guard any direct writes to stdout/stderr:

```python
# Bad - crashes under pythonw
print("status update")

# Good - check first, or just use logging
if sys.stdout is not None:
    print("status update")
```

Wrap `main()` in try/except that writes to a fallback error file -- unhandled exceptions vanish silently under pythonw.

## Subprocess Safety

Prefer list args over string interpolation. Avoid `shell=True`:

```python
# Bad - command injection via user input
subprocess.run(f"process {user_input}", shell=True)

# Good - list args, no shell
subprocess.run(["process", user_input])
```

## pip Invocation

`python -m pip` is more portable than calling `pip.exe` directly:

```python
# Bad - assumes pip is on PATH or in Scripts/
subprocess.run(["pip", "install", "watchdog"])

# Good - uses the pip associated with the current Python
subprocess.run([sys.executable, "-m", "pip", "install", "watchdog"])
```

This works across virtualenvs, conda, system installs, and Windows store Python.

## File Locking on Windows

Python's `open()` uses shared access on Windows -- it won't detect files being written by other processes. For exclusive lock checks, use `ctypes` to call Win32 `CreateFileW`:

```python
import ctypes
import ctypes.wintypes

GENERIC_READ = 0x80000000
OPEN_EXISTING = 3
FILE_SHARE_NONE = 0  # exclusive -- fails if anyone else has it open
INVALID_HANDLE_VALUE = ctypes.wintypes.HANDLE(-1).value

CreateFileW = ctypes.windll.kernel32.CreateFileW
CreateFileW.argtypes = [
    ctypes.wintypes.LPCWSTR, ctypes.wintypes.DWORD, ctypes.wintypes.DWORD,
    ctypes.c_void_p, ctypes.wintypes.DWORD, ctypes.wintypes.DWORD,
    ctypes.wintypes.HANDLE,
]
CreateFileW.restype = ctypes.wintypes.HANDLE

handle = CreateFileW(
    str(file_path), GENERIC_READ, FILE_SHARE_NONE,
    None, OPEN_EXISTING, 0, None
)
if handle == INVALID_HANDLE_VALUE:
    # File is locked by another process
    pass
else:
    ctypes.windll.kernel32.CloseHandle(handle)
    # File is ready
```

This is the Python equivalent of .NET's `FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)`.

## Package Path Constants

When a Python package needs to reference a project root or data directory, define the path once in a central config module rather than computing it per-file:

```python
# config.py — adjust parent depth to match your layout
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent.parent

# other modules
from .config import PROJECT_ROOT
```

This prevents drift when directory depth changes (e.g., after refactoring a flat script into a package).
