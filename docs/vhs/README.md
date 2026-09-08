# README demo GIFs

The animated GIFs in the project README are generated from the [`.tape`](https://github.com/charmbracelet/vhs) scripts in this directory. The rendered GIFs are **not** stored in this repository: they live in the separate [`Refedle-Assets`](https://github.com/Yuta-K19418/Refedle-Assets) repo, and `README.md` references them by raw URL pinned to that repo's `main` branch. This keeps regenerated binaries out of this repo's history.

## Regenerating

Clone `Refedle-Assets` next to this repo once:

```bash
git clone https://github.com/Yuta-K19418/Refedle-Assets.git ../Refedle-Assets
```

Then:

```bash
./docs/vhs/regen.sh             # regenerate every GIF
./docs/vhs/regen.sh hero apply  # regenerate only the named ones

cd ../Refedle-Assets            # publish
git add images && git commit -m "docs: regenerate <name> for <UI change>"
git push
```

`regen.sh` publishes the app with Native AOT, stages the sample data in a
throwaway working directory, runs each tape through `vhs`, optimizes the result
with `gifsicle`, and writes it into the `Refedle-Assets` clone's `images/`
directory (override the location with `REFEDLE_ASSETS_DIR`). The recordings run
the real published `refedle` binary (on `PATH`, outside the working directory so
it never appears in the app's file dialogs), so the TUI starts as fast on screen
as it does for end users.

Because the README URLs are pinned to `main`, a push to `Refedle-Assets` is all
it takes for the README to pick up the new GIF — no change in this repo.

Requirements: the .NET SDK (see [`global.json`](../../global.json)) and the
Native AOT toolchain (a C toolchain and `libicu`), plus
[`vhs`](https://github.com/charmbracelet/vhs), `ttyd`, `ffmpeg`, and `gifsicle`
on `PATH`.

## When to regenerate

**Only when a UI change actually changes what a GIF shows** — a new key binding
in a demoed flow, a restyled dialog, a changed status bar, a new column in the
sample output. Do **not** rerun it for unrelated changes: each regeneration adds
the GIF's full size to `Refedle-Assets`'s history (splitting the binaries into
their own repo is what keeps that churn out of this one). `regen.sh` is
deliberately **not** wired into CI (VHS output is not byte-reproducible, so a
freshness check would only produce false diffs).

When a PR relies on a regenerated GIF, say in the description which UI change it
reflects and link the `Refedle-Assets` commit.

## Size budget

`gifsicle` is tuned per GIF in `regen.sh` (`--lossy`, `--colors`). Targets:

| GIF | Budget |
|---|---|
| `hero` | ≤ 800 KB |
| `apply`, table-action feature GIFs (`rename`, `filter`, `fill`, `format-timestamp`) | ≤ 400 KB each |
| `drilldown-*` (mostly static) | ≤ 150 KB each |

If a GIF blows its budget: shorten the tape (trim `Sleep`s, cut redundant
moves), lower its `--lossy` / `--colors` entry in `regen.sh`, drop `Set
Framerate`, or narrow `Set Width`.

## Files

| File | Purpose |
|---|---|
| `*.tape` | one VHS script per GIF |
| `regen.sh` | render + optimize + write into the `Refedle-Assets` clone's `images/` |
| `data/people.csv` | shared sample data (`name,age,email,city,signup`) |
| `data/people.yaml` | the recipe `hero.tape` records and `apply.tape` replays (Filter `age == 30`) |
| `data/orders.json` | JSON Array sample for `drilldown-aggregate.tape` |
| `data/account.json` | JSON Object sample for `drilldown-node.tape` |

Tapes are portable: they assume the working directory holds the sample data and
a `refedle` on `PATH`, both of which `regen.sh` provides.

The AOT `refedle` binary starts in well under a second, so a `Sleep 1100ms`
after launching the TUI is enough for the table to finish painting before the
tape sends any keys. Keep that value identical across tapes so the GIFs feel
consistent. (VHS `Sleep` does not "wait for the program" - it just records idle
frames - so sending keystrokes earlier would lose them.)
