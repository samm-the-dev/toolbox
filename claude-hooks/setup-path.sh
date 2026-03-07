#!/bin/bash
# SessionStart: Ensure CLI tools are available in Claude Code's bash shell.
# Add directories here as needed when tools aren't found in PATH.
#
# Register in ~/.claude/settings.json:
#   "SessionStart": [{
#     "matcher": "",
#     "hooks": [{ "type": "command", "command": "~/.claude/hooks/setup-path.sh" }]
#   }]
#
# Customize the paths below for your machine. Tools added to
# CLAUDE_ENV_FILE persist for the entire Claude Code session.

if [ -n "$CLAUDE_ENV_FILE" ]; then
  # GitHub CLI (Windows default install location)
  if [ -d "/c/Program Files/GitHub CLI" ]; then
    echo 'export PATH="$PATH:/c/Program Files/GitHub CLI"' >> "$CLAUDE_ENV_FILE"
  fi

  # npm global packages
  if [ -d "$APPDATA/npm" ]; then
    echo "export PATH=\"\$PATH:$APPDATA/npm\"" >> "$CLAUDE_ENV_FILE"
  fi

  # Google Cloud SDK (user-level install default location)
  if [ -d "$LOCALAPPDATA/Google/Cloud SDK/google-cloud-sdk/bin" ]; then
    echo "export PATH=\"\$PATH:$LOCALAPPDATA/Google/Cloud SDK/google-cloud-sdk/bin\"" >> "$CLAUDE_ENV_FILE"
  fi

  # Python (update version number as needed)
  # if [ -d "/c/Program Files/Python313" ]; then
  #   echo 'export PATH="$PATH:/c/Program Files/Python313"' >> "$CLAUDE_ENV_FILE"
  # fi

  # Add more tools as needed:
  # if [ -d "/path/to/tool" ]; then
  #   echo 'export PATH="$PATH:/path/to/tool"' >> "$CLAUDE_ENV_FILE"
  # fi
fi

exit 0
