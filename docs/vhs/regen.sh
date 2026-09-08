#!/usr/bin/env bash
#
# Regenerate the README demo GIFs from the .tape files in this directory.
#
#   ./docs/vhs/regen.sh              # regenerate every GIF
#   ./docs/vhs/regen.sh hero apply   # regenerate only the named ones
#
# The GIFs live in a separate repo (Yuta-K19418/Refedle-Assets), cloned next to
# this one, so regenerating them on a UI change does not grow this repo's history
# with binary blobs. Set REFEDLE_ASSETS_DIR to point elsewhere. After a run,
# commit and push in that clone; the README references it by raw URL on `main`.
#
# Only run this when a UI change actually alters what a GIF shows. This script is
# intentionally NOT wired into CI.
#
# Requirements: dotnet SDK, vhs, ttyd, ffmpeg, gifsicle, and the Native AOT
# toolchain (a C toolchain plus libicu - the recordings run the real published
# `refedle` binary, so the TUI starts as fast as it does for end users).

set -euo pipefail

repo_root=$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)
vhs_dir="$repo_root/docs/vhs"
app_csproj="$repo_root/src/App/Refedle.App.csproj"

# The rendered GIFs go into the sibling Refedle-Assets clone, not this repo.
assets_dir="${REFEDLE_ASSETS_DIR:-$(dirname "$repo_root")/Refedle-Assets}"
out_dir="$assets_dir/images"
[ -d "$assets_dir/.git" ] || {
    echo "error: Refedle-Assets clone not found at '$assets_dir'." >&2
    echo "       git clone https://github.com/Yuta-K19418/Refedle-Assets.git next to this repo," >&2
    echo "       or set REFEDLE_ASSETS_DIR to its path." >&2
    exit 1
}
rid=$(dotnet --info 2>/dev/null | sed -n 's/^ *RID: *//p' | head -1)

for tool in dotnet vhs ttyd ffmpeg gifsicle; do
    command -v "$tool" >/dev/null 2>&1 || { echo "error: '$tool' not found on PATH" >&2; exit 1; }
done

# Per-GIF gifsicle budget knobs. Terminal UIs compress well; --lossy trades a
# little dithering noise for a big size win. Keep hero <= 800KB, the rest small.
declare -A LOSSY=( [hero]=60 [apply]=80 )
declare -A COLORS=( [hero]=96 [apply]=96 [filter]=48 )
default_lossy=100
default_colors=64

staging=$(mktemp -d)
# A short, readable working directory: the app's Save Recipe dialog shows the
# current path for a moment, and a random mktemp name looks like a leak on screen.
demo_dir="${TMPDIR:-/tmp}/refedle-demo"
trap 'rm -rf "$staging" "$demo_dir"' EXIT

echo "==> publishing $app_csproj (Native AOT, $rid)"
dotnet publish "$app_csproj" -c Release -r "$rid" -o "$staging/bin" -v quiet
# The published `refedle` binary goes on PATH from a directory OUTSIDE the working
# directory, so it never shows up in the app's own file dialogs during a recording.
export PATH="$staging/bin:$PATH"

if [ "$#" -gt 0 ]; then
    tapes=("$@")
else
    tapes=()
    for t in "$vhs_dir"/*.tape; do
        tapes+=("$(basename "$t" .tape)")
    done
fi

mkdir -p "$out_dir"
for name in "${tapes[@]}"; do
    tape="$vhs_dir/$name.tape"
    [ -f "$tape" ] || { echo "error: no tape for '$name' ($tape)" >&2; exit 1; }

    # Fresh working directory per tape: no cross-tape ordering effects (e.g. one
    # tape overwriting people.yaml with a new timestamp that another then shows).
    work="$demo_dir"
    rm -rf "$work"
    mkdir -p "$work"
    cp "$vhs_dir"/data/* "$work/"

    echo "==> $name"
    ( cd "$work" && vhs "$tape" )

    raw="$work/$name.gif"
    lossy=${LOSSY[$name]:-$default_lossy}
    colors=${COLORS[$name]:-$default_colors}
    gifsicle -O3 --lossy="$lossy" --colors "$colors" "$raw" -o "$out_dir/$name.gif"

    size=$(du -h "$out_dir/$name.gif" | cut -f1)
    echo "    $out_dir/$name.gif  ($size)"
done

echo "==> done. Commit and push in $assets_dir to publish."
