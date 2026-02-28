# Shell & Path Handling

Cross-platform reference for working on Windows with bash-like shells (Git Bash, WSL, MSYS2). For core rules, see [AGENTS.md](AGENTS.md).

## Cross-Platform Paths

- Use Unix-style paths: `/c/Users/name` not `C:\Users\name`
- Forward slashes work universally: `cd /c/Dev/project`
- Avoid Windows path literals in shell commands

## Escaping Pitfalls

**Backticks:** Interpreted as command substitution
```bash
# Bad - backticks execute as command
gh api ... -f body="Fix the `error` handling"

# Good - use single quotes or escape
gh api ... -f body='Fix the error handling'
```

**Dollar signs:** Variable expansion
```bash
# Bad - $variable expands (usually to empty)
echo "Cost is $50"

# Good - escape or single-quote
echo "Cost is \$50"
echo 'Cost is $50'
```

**Quotes in JSON:** Double-escape or use heredocs
```bash
# Heredoc for complex content
git commit -m "$(cat <<'EOF'
Message with "quotes" and $pecial chars
EOF
)"
```

**Newlines:** Don't use literal newlines to chain commands
```bash
# Bad - newline breaks command
cd /path
npm install

# Good - && chains or separate calls
cd /path && npm install
```

## Common Fixes

| Issue | Solution |
|-------|----------|
| Path not found (Windows) | Use `/c/path` not `c:\path` |
| Command not found | Check PATH, use full path |
| Unexpected token | Escape special chars (`$`, `` ` ``, `"`) |
| Heredoc issues | Use `<<'EOF'` (quoted) to prevent expansion |
| Unexpected EOF with quoted paths | Trailing `\` before `"` escapes the quote; use PowerShell or forward slashes |

**Trailing backslash issue:** Windows paths ending in `\` cause "unexpected EOF" errors:
```bash
# Bad - \' escapes the closing quote
ls "C:\path\to\dir\"

# Good - use PowerShell for Windows paths
powershell -Command "Get-ChildItem 'C:\path\to\dir'"

# Good - or use forward slashes
ls "C:/path/to/dir/"
```

## PowerShell from Bash

When calling PowerShell from bash-like shells, variable syntax gets mangled:

```bash
# Bad - $_ becomes 'extglob' or similar garbage
powershell -Command "Get-ChildItem | Where-Object { $_.Name -like '*foo*' }"

# Good - avoid inline PowerShell filtering, use simpler commands
powershell -Command "Get-ChildItem -Filter '*foo*'"

# Good - or pipe through bash tools
powershell -Command "Get-ChildItem" | grep foo
```

The `$_` variable (and other `$` variables) in PowerShell commands passed through bash get interpreted by bash first, causing errors like `extglob.Name: command not found`.

**Workarounds:**
- Use PowerShell's parameter-based filtering (`-Filter`, `-Include`) instead of `Where-Object`
- Use simpler PowerShell commands and filter with bash tools (`grep`, `awk`)
- For complex PowerShell logic, write a `.ps1` script file and invoke it:

```bash
# Write script to scratchpad (or temp dir)
cat > /tmp/my-script.ps1 <<'EOF'
$value = [Environment]::GetEnvironmentVariable('Path', 'User')
Write-Host "Current: $value"
EOF

# Execute — runs in pure PowerShell context, no escaping issues
powershell -ExecutionPolicy Bypass -File /tmp/my-script.ps1
```

This is the most reliable approach for anything involving `$` variables, `Where-Object`, or multi-line logic.

## PowerShell Encoding Gotcha

PowerShell 5.1's `-Encoding UTF8` writes a UTF-8 BOM (byte order mark). Other languages reading the output may choke:

- **Python**: `open(path, encoding="utf-8")` then `json.load(f)` raises `JSONDecodeError`. Use `open(path, encoding="utf-8-sig")` which strips the BOM transparently.
- **Node.js**: `JSON.parse(fs.readFileSync(..., 'utf-8'))` fails. Strip with `.replace(/^\uFEFF/, '')`.

PowerShell 7+ defaults to BOM-less UTF-8. This only affects PS 5.1 (ships with Windows, still the default in many environments).

## Cross-Platform Tool Availability

Some Unix tools aren't available by default on Windows:

| Tool | Status | Alternative |
|------|--------|-------------|
| `jq` | Not on Windows Git Bash | Use `--jq` flag with `gh` CLI, or PowerShell's `ConvertFrom-Json` |
| `sed` | Limited in Git Bash | Use dedicated Edit tool or PowerShell |
| `awk` | Limited in Git Bash | Use dedicated tools or scripting |

Prefer tool-native filtering (e.g., `gh api --jq '.field'`) over piping to `jq`.

## gh API Path Gotcha (Git Bash / MSYS2)

Git Bash (MSYS2) rewrites arguments starting with `/` as Windows paths. Omit the leading slash in `gh api` calls:

```bash
# Wrong — Git Bash mangles the path
gh api /repos/OWNER/REPO/pulls

# Correct
gh api repos/OWNER/REPO/pulls
```
