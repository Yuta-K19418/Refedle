# Benchmarks

How Refedle compares with other terminal data viewers when opening large files. The tools compared and the measurement method are described per file format.

## Environment

- **Machine**: Apple M3, 16 GB RAM, macOS 27.0.1
- **Data**: generated with [`benchmarks/data/generate_data.py`](../benchmarks/data/generate_data.py) (`--seed 42`, so the same data can be regenerated at any time). Sizes use binary units (1 GB = 1024³ bytes).

## CSV

### Tools Compared

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

### Method

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

### Data

8 columns, about 177 bytes per row: 581,811 rows (100 MB), 5,860,049 rows (1 GB), 57,659,233 rows (10 GB).

### Results

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

### Reproducing

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

Quit with `q` as soon as the full load indicator (see [Method](#method)) appears.
