# Installation

## Manual download

Prebuilt binaries (no .NET SDK required) are published on the [Releases page](https://github.com/Yuta-K19418/Refedle/releases) for:

| OS | Architecture |
|---|---|
| Windows | x64 |
| macOS | Apple Silicon (arm64) |
| Linux | x64 |
| Linux | arm64 |

macOS on Intel (`osx-x64`) is not supported; build from source with `dotnet publish src/App/Refedle.App.csproj -r osx-x64 -c Release`.

Download the archive for your platform and `checksums.txt`, verify, extract, and put `refedle` on your `PATH` (Linux x64 example):

```bash
tag=v0.3.0
base=https://github.com/Yuta-K19418/Refedle/releases/download/$tag
curl -fLO $base/refedle-$tag-linux-x64.tar.gz
curl -fLO $base/checksums.txt
sha256sum -c --ignore-missing checksums.txt
tar -xzf refedle-$tag-linux-x64.tar.gz
install -Dm755 refedle-$tag-linux-x64/refedle ~/.local/bin/refedle
```

On Windows, download `refedle-<tag>-win-x64.zip`, extract it, and move `refedle.exe` onto your `PATH`.

The binaries are unsigned, so the OS may block them on first launch:

- **macOS**: Gatekeeper quarantines downloaded files. Run `xattr -d com.apple.quarantine refedle` before launching, or allow it via System Settings → Privacy & Security.
- **Windows**: SmartScreen may warn about an unrecognized app. Click "More info" → "Run anyway".

Run the binary directly as `./refedle [--file <path>] [--recipe <path.yaml>]` (or `refedle.exe` on Windows). Building from source instead? Replace `refedle` with `dotnet run --project src/App --` in the [TUI usage guide](tui.md) and [CLI usage guide](cli.md) examples.
