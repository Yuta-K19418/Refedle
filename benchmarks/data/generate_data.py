"""Generate deterministic test data for the benchmarks in docs/benchmarks.md.

Usage:
    python3 benchmarks/data/generate_data.py --format csv --size 1GB
"""

import argparse
import random
import string
import sys
import time
from datetime import datetime, timedelta, timezone

# Sizes are binary units (1GB = 1024^3 bytes).
UNITS = {"KB": 1024, "MB": 1024**2, "GB": 1024**3}
EXTENSIONS = {"csv": "csv"}
BATCH_SIZE = 10_000
BASE_TIME = datetime(2026, 1, 1, tzinfo=timezone.utc)


def parse_size(text):
    for unit, factor in UNITS.items():
        if text.upper().endswith(unit):
            return int(float(text[: -len(unit)]) * factor)
    raise ValueError(f"invalid size: {text} (use e.g. 100MB, 1GB)")


def random_string(rng, length):
    return "".join(rng.choices(string.ascii_letters + string.digits, k=length))


class RecordFactory:
    def __init__(self, rng):
        self._rng = rng
        # Reuse pooled strings; generating fresh random text per row is too slow.
        self._payloads = [random_string(rng, 64) for _ in range(1000)]
        self._tags = [random_string(rng, 8) for _ in range(500)]
        self._categories = ["A", "B", "C", "D"]

    def create(self, record_id):
        rng = self._rng
        return {
            "id": record_id,
            "name": f"User {record_id}",
            "email": f"user{record_id}@example.com",
            "category": rng.choice(self._categories),
            "score": round(rng.uniform(0, 100), 2),
            "tags": rng.sample(self._tags, 5),
            "payload": rng.choice(self._payloads),
            "timestamp": (BASE_TIME + timedelta(seconds=record_id)).strftime("%Y-%m-%dT%H:%M:%SZ"),
        }


def csv_header():
    return "id,name,email,category,score,tags,payload,timestamp\n"


def csv_line(record):
    # Generated values never contain commas, quotes, or newlines, so no quoting is needed.
    return (
        f"{record['id']},{record['name']},{record['email']},{record['category']},"
        f"{record['score']},{'|'.join(record['tags'])},{record['payload']},{record['timestamp']}\n"
    )


def generate(fmt, target_bytes, output, seed):
    factory = RecordFactory(random.Random(seed))
    record_id = 0
    written = 0
    start = time.time()
    last_log = start

    with open(output, "wb", buffering=16 * 1024 * 1024) as f:
        header = csv_header().encode()
        f.write(header)
        written += len(header)

        while written < target_bytes:
            chunk = []
            for _ in range(BATCH_SIZE):
                record_id += 1
                line = csv_line(factory.create(record_id)).encode()
                chunk.append(line)
                written += len(line)
                if written >= target_bytes:
                    break
            f.write(b"".join(chunk))

            now = time.time()
            if now - last_log >= 10:
                print(
                    f"{written / target_bytes * 100:.1f}% ({written / 1024**3:.2f} GB, {record_id:,} rows)",
                    file=sys.stderr,
                    flush=True,
                )
                last_log = now

    print(
        f"Wrote {output}: {written:,} bytes, {record_id:,} rows in {time.time() - start:.1f}s",
        file=sys.stderr,
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--format", choices=EXTENSIONS.keys(), required=True)
    parser.add_argument("--size", required=True, help="target size, e.g. 100MB, 1GB, 10GB")
    parser.add_argument("--output", help="output path (default: bench_<size>.local.<ext>)")
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()

    try:
        target_bytes = parse_size(args.size)
    except ValueError as e:
        parser.error(str(e))

    output = args.output or f"bench_{args.size.lower()}.local.{EXTENSIONS[args.format]}"
    generate(args.format, target_bytes, output, args.seed)


if __name__ == "__main__":
    main()
