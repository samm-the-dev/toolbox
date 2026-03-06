# Claude Code LSP Setup

LSP gives Claude Code semantic code navigation (go-to-definition, find-references, hover, diagnostics) instead of text-based grep. Works in both the CLI and VSCode extension — Claude Code runs its own language servers, independent of the host IDE.

## 1. Enable the LSP Tool

Add to `~/.claude/settings.json`:

```json
{
  "env": {
    "ENABLE_LSP_TOOL": "1"
  }
}
```

## 2. Install Language Server Binaries

Install the system binaries for the languages you need:

### TypeScript / JavaScript

```bash
npm install -g typescript-language-server typescript
```

### Python

```bash
npm install -g pyright
```

### C#

Requires .NET SDK 6.0+:

```bash
dotnet tool install --global csharp-ls
```

### Other Languages

See the [official plugin marketplace](https://github.com/anthropics/claude-plugins-official) for Go (`gopls`), Rust (`rust-analyzer`), Java (`jdtls`), Kotlin, PHP, Ruby, Lua, Swift, and HTML/CSS.

## 3. Install Plugins

From the CLI, install the corresponding Claude Code plugin for each language:

```bash
claude plugin install typescript-lsp@claude-plugins-official --scope user
claude plugin install pyright-lsp@claude-plugins-official --scope user
claude plugin install csharp-lsp@claude-plugins-official --scope user
```

Plugins are available in the `claude-plugins-official` marketplace (auto-registered). Use `--scope project` to share via version control or `--scope user` for personal global install.

## 4. Restart Claude Code

LSP servers initialize at startup. A full restart is required after installing plugins or binaries.

## 5. Verify

After restart, the LSP tool should appear in the tool list. Test with any operation:

```
LSP hover on src/App.tsx line 1, character 10
```

If working, you'll see type information and/or diagnostics. If you see "No LSP server available", check that the binary is on your PATH.

## Troubleshooting

| Problem | Fix |
|---|---|
| "No LSP server available" | Binary not installed or not on PATH. Run `which typescript-language-server` (or equivalent) to check. |
| "Executable not found in $PATH" | Same as above. Ensure global npm bin is on PATH (`npm config get prefix` + `/bin`). |
| LSP tool not in tool list | `ENABLE_LSP_TOOL` not set, or restart needed. Check `~/.claude/settings.json`. |
| Diagnostics show missing modules | Expected if `node_modules` aren't installed in the project. Run `npm install` in the project root. |
| C# LSP not working | Needs .NET SDK (not just runtime). Check with `dotnet --list-sdks`. |

## Notes

- Each LSP server uses 200-500MB of memory. Only install languages you actively use.
- The VSCode extension does **not** share VSCode's built-in language servers. There's an [open feature request](https://github.com/anthropics/claude-code/issues/24249) for this.
- The plugin system caches plugins in `~/.claude/plugins/cache/`. Use `claude plugin update` to refresh.
