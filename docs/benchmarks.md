# Benchmarks

How Refedle compares with other terminal data viewers when opening large files. The tools compared and the measurement method are described per file format.

## Environment

- **Machine**: Apple M3, 16 GB RAM, macOS 27.0.1
- **Data**: generated with [`benchmarks/data/generate_data.py`](../benchmarks/data/generate_data.py) (`--seed 42`, so the same data can be regenerated at any time). Sizes use binary units (1 GB = 1024³ bytes).

## CSV

### Tools Compared (CSV)

| Tool | Version | How it loads a file |
|---|---|---|
| Refedle | 0.10.6 | Indexes the file in the background and reads rows on demand |
| csvlens | 0.15.1 | Counts rows in the background and reads rows on demand |
| Tabiew | 0.15.1 | Loads the whole file into memory as a Polars DataFrame |
| VisiData | 3.4 | Loads all rows into memory, showing them as they load |

Strengths of the other tools:

- **csvlens**: A focused CSV viewer with regex search, filtering, and sorting. Uses the least memory of the four.
- **Tabiew**: SQL queries and plotting, with support for many formats such as Parquet, Arrow, and Excel.
- **VisiData**: Far more feature-rich than Refedle, with frequency tables and support for a very wide range of formats such as SQLite, Excel, and HDF5.

Holding every row in memory is what makes much of VisiData's and Tabiew's functionality possible; the trade-off shows up with very large files.

### Method (CSV)

- **First row**: whether the first rows appear immediately or only after the whole file is loaded.
- **Full load**: time until the tool's own on-screen indicator (below) shows that the whole file has been read. Measured as `real` from `/usr/bin/time -l`, quitting with `q` right after the indicator appears, so it includes human reaction time (a few hundred milliseconds). Sub-second differences are not meaningful.
- **Memory**: `peak memory footprint` from `/usr/bin/time -l`, taken from the first cold run.
- **Cache**: *cold* runs `sudo purge` before each run; *warm* runs read a file already in the OS cache. Times are the median of 3 runs.
- Runs that did not finish loading, or stopped responding, were aborted after one attempt and are marked "did not finish".

| Tool | Full load indicator |
|---|---|
| Refedle | The progress bar disappears and the row count is shown |
| csvlens | The `+` after the row count disappears |
| Tabiew | The table appears (it is shown only after loading) |
| VisiData | The progress indicator reaches 100% |

### Data (CSV)

8 columns, about 177 bytes per row: 581,811 rows (100 MB), 5,860,049 rows (1 GB), 57,659,233 rows (10 GB).

### Results (CSV)

**First row**: immediate for Refedle, csvlens, and VisiData. Tabiew shows the table only after the full load.

**Full load (seconds, median; cold / warm)**

| Tool | 100 MB | 1 GB | 10 GB |
|---|---|---|---|
| Refedle | 1.1 / 0.6 | 1.7 / 0.8 | 5.4 / 4.5 |
| csvlens | 1.6 / 1.0 | 2.1 / 1.5 | 10.7 / 10.2 |
| Tabiew | 1.7 / 0.7 | 5.0 / 2.7 | did not finish |
| VisiData | 2.3 / 2.0 | 15.1 / 14.5 | did not finish |

**Peak memory footprint**

| Tool | 100 MB | 1 GB | 10 GB |
|---|---|---|---|
| Refedle | 24 MB | 25 MB | 26 MB |
| csvlens | 6 MB | 5 MB | 5 MB |
| Tabiew | 269 MB | 2.4 GB | did not finish (22.6 GB when stopped) |
| VisiData | 405 MB | 3.7 GB | did not finish (18.9 GB when stopped) |

- At 100 MB, Refedle and csvlens finish loading almost instantly, so their times are mostly reaction time.
- After indexing, Refedle runs a background scan to refine the schema. Its peak memory stays the same after that scan has also finished.
- At 10 GB, Tabiew still showed no table after about 2 minutes. It was stopped with Ctrl+C because the Mac's memory usage had passed 15 GB and the system was at risk of becoming unresponsive. VisiData stopped responding to input after about 4 minutes and was terminated. Both had exceeded the machine's 16 GB of physical memory.

### Reproducing (CSV)

Generate the data (sizes: `100MB`, `1GB`, `10GB`):

```bash
python3 benchmarks/data/generate_data.py --format csv --size 1GB
```

Open it with each tool (cold; for warm, drop `sudo purge &&`):

```bash
sudo purge && /usr/bin/time -l refedle --file bench_1gb.local.csv
sudo purge && /usr/bin/time -l csvlens bench_1gb.local.csv
sudo purge && /usr/bin/time -l tw bench_1gb.local.csv
sudo purge && /usr/bin/time -l vd bench_1gb.local.csv
```

Quit with `q` as soon as the full load indicator (see [Method](#method-csv)) appears.

## JSON Lines

### Tools Compared (JSON Lines)

| Tool | Version | How it loads a file |
|---|---|---|
| Refedle | 0.10.6 | Indexes the file in the background and reads rows on demand |
| Tabiew | 0.15.1 | Loads the whole file into memory as a Polars DataFrame |
| VisiData | 3.4 | Loads all rows into memory, showing them as they load |
| jless | 0.9.0 | Reads the whole file before showing it |
| fx | 40.0.0 | Shows the screen immediately and reads the whole file in the background |

Strengths of the other tools:

- **Tabiew**: SQL queries and plotting, with support for many formats such as Parquet, Arrow, and Excel.
- **VisiData**: Far more feature-rich than Refedle, with frequency tables and support for a very wide range of formats such as SQLite, Excel, and HDF5.
- **jless**: Browse JSON as a collapsible tree with Vim-style navigation and regex search.
- **fx**: Transform JSON with JavaScript expressions, with support for formats such as YAML and TOML, and path and query autocomplete.

Holding the whole file in memory is what makes much of these tools' functionality possible; the trade-off shows up with very large files.

### Method (JSON Lines)

- **First row**: whether the first rows appear immediately or only after the whole file is loaded.
- **Full load**: time until the tool's own on-screen indicator (below) shows that the whole file has been read. Measured as `real` from `/usr/bin/time -l`, quitting with `q` right after the indicator appears, so it includes human reaction time (a few hundred milliseconds). Sub-second differences are not meaningful.
- **Memory**: `peak memory footprint` from `/usr/bin/time -l`, taken from the first cold run.
- **Cache**: *cold* runs `sudo purge` before each run; *warm* runs read a file already in the OS cache. Times are the median of 3 runs.
- A run was stopped and marked "did not finish" if loading had not finished after 5 minutes, or if the Mac's memory usage passed 15 GB. Such runs were tried only once.

| Tool | Full load indicator |
|---|---|
| Refedle | The progress bar disappears and the row count is shown |
| Tabiew | The table appears (it is shown only after loading) |
| VisiData | The progress indicator reaches 100% |
| jless | The screen appears (it is shown only after loading) |
| fx | The spinner disappears and the scroll position (`0%`) is shown |

### Data (JSON Lines)

8 fields per row, with `tags` as an array and `address` as a nested object, about 316 bytes per row: 334,554 rows (100 MB), 3,393,178 rows (1 GB), 33,613,943 rows (10 GB).

### Results (JSON Lines)

**First row**: immediate for Refedle, VisiData, and fx. Tabiew and jless show the file only after the full load.

**Full load (seconds, median; cold / warm)**

| Tool | 100 MB | 1 GB | 10 GB |
|---|---|---|---|
| Refedle | 1.2 / 0.6 | 2.1 / 0.9 | 5.3 / 4.5 |
| Tabiew | 1.6 / 1.1 | 10.2 / 8.2 | did not finish |
| VisiData | 3.0 / 2.4 | 23.0 / 22.5 | did not finish |
| jless | 1.4 / 0.7 | 3.5 / 2.8 | did not finish |
| fx | 29.7 / 29.7 | about 360 (cold, 1 run) | not run |

**Peak memory footprint**

| Tool | 100 MB | 1 GB | 10 GB |
|---|---|---|---|
| Refedle | 24 MB | 26 MB | 26 MB |
| Tabiew | 385 MB | 4.0 GB | did not finish (35.2 GB when stopped) |
| VisiData | 690 MB | 6.5 GB | did not finish (18.9 GB when stopped) |
| jless | 456 MB | 5.1–5.7 GB | did not finish (26.3 GB when stopped) |
| fx | 2.1 GB | 20.7 GB | not run |

- At 100 MB, Refedle and jless finish loading almost instantly, so their times are mostly reaction time.
- jless's memory at 1 GB was either 5.1 GB or 5.7 GB, depending on the run.
- When switched to Table mode, Refedle scans the whole file in the background to refine the schema. After that scan had finished, its peak memory was at most 32 MB (at 10 GB).
- fx finished loading 1 GB in about 6 minutes. That is past the 5-minute limit, but since it did finish, the result is shown. Because one run takes about 6 minutes and cold and warm times were the same at 100 MB, it was measured with a single cold run. Its memory reached 20.7 GB, more than the machine's 16 GB of physical memory, so 10 GB was not run.
- At 10 GB, the other tools were stopped before they finished loading. All of them had exceeded the machine's 16 GB of physical memory:
  - Tabiew still showed no table after about 2 minutes. It was stopped with Ctrl+C because the Mac's memory usage had passed 15 GB.
  - jless still showed no screen after about 1 minute. It was terminated because the Mac's memory usage had passed 15 GB.
  - VisiData was terminated after about 2 minutes, when the Mac's memory usage had passed 14 GB and its footprint had reached 18.9 GB.

### Reproducing (JSON Lines)

Generate the data (sizes: `100MB`, `1GB`, `10GB`):

```bash
python3 benchmarks/data/generate_data.py --format jsonl --size 1GB
```

Open it with each tool (cold; for warm, drop `sudo purge &&`):

```bash
sudo purge && /usr/bin/time -l refedle --file bench_1gb.local.jsonl
sudo purge && /usr/bin/time -l tw bench_1gb.local.jsonl
sudo purge && /usr/bin/time -l vd bench_1gb.local.jsonl
sudo purge && /usr/bin/time -l jless bench_1gb.local.jsonl
sudo purge && /usr/bin/time -l fx bench_1gb.local.jsonl
```

Quit with `q` as soon as the full load indicator (see [Method](#method-json-lines)) appears.

## JSON Array

### Tools Compared (JSON Array)

The tools, versions, how they load a file, and their strengths are the same as for [JSON Lines](#tools-compared-json-lines).

### Method (JSON Array)

The method and full load indicators are the same as for [JSON Lines](#method-json-lines), except that a run where the tool exited with an error is marked "error".

### Data (JSON Array)

The same records as the JSON Lines data, in a single array with one record per line: 333,493 rows (100 MB), 3,382,522 rows (1 GB), 33,509,364 rows (10 GB).

### Results (JSON Array)

**First row**: immediate for Refedle, VisiData, and fx. Tabiew and jless show the file only after the full load.

**Full load (seconds, median; cold / warm)**

| Tool | 100 MB | 1 GB | 10 GB |
|---|---|---|---|
| Refedle | 1.4 / 0.9 | 2.9 / 2.3 | 17.5 / 17.0 |
| Tabiew | 1.7 / 1.2 | 10.6 / 8.5 | error |
| VisiData | 1.9 / 1.3 | 11.6 / 10.5 | did not finish |
| jless | 1.3 / 0.8 | 3.6 / 2.8 | did not finish |
| fx | 1.6 / 1.4 | 13.4 / 14.4 | did not finish |

**Peak memory footprint**

| Tool | 100 MB | 1 GB | 10 GB |
|---|---|---|---|
| Refedle | 25 MB | 26 MB | 26 MB |
| Tabiew | 1.1 GB | 9.1 GB | error (10.0 GB at exit) |
| VisiData | 711 MB | 6.8 GB | did not finish (15.5 GB when stopped) |
| jless | 453 MB | 5.1–7.9 GB | did not finish (18.8 GB when stopped) |
| fx | 1.4 GB | 13.8 GB | did not finish (40.5 GB when stopped) |

- At 100 MB, every tool finishes loading almost instantly, so the differences are mostly reaction time.
- jless's memory at 1 GB was 5.1 GB, 5.7 GB, or 7.9 GB, depending on the run.
- At 10 GB, none of the other tools finished loading:
  - Tabiew exited with an `InputTooLarge` error after about 26 seconds, without showing a table.
  - VisiData was force quit from Activity Monitor because the Mac's memory usage had passed 15 GB.
  - jless still showed no screen after about 50 seconds. It was terminated because the Mac's memory usage had passed 15 GB (footprint 18.8 GB).
  - fx was still showing its spinner after about 1.5 minutes. It was terminated because the Mac's memory usage had passed 15 GB (footprint 40.5 GB).

### Reproducing (JSON Array)

Generate the data (sizes: `100MB`, `1GB`, `10GB`):

```bash
python3 benchmarks/data/generate_data.py --format json-array --size 1GB
```

Open it with each tool (cold; for warm, drop `sudo purge &&`):

```bash
sudo purge && /usr/bin/time -l refedle --file bench_1gb.local.array.json
sudo purge && /usr/bin/time -l tw bench_1gb.local.array.json
sudo purge && /usr/bin/time -l vd bench_1gb.local.array.json
sudo purge && /usr/bin/time -l jless bench_1gb.local.array.json
sudo purge && /usr/bin/time -l fx bench_1gb.local.array.json
```

Quit with `q` as soon as the full load indicator (see [Method](#method-json-lines)) appears.
