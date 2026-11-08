"""Profile every CSV in data/raw: rows, columns, dtypes, nulls, distinct counts, key uniqueness.
Usage (from project root):  python scripts/profile_data.py [raw_dir]
Requires: pip install pandas
"""
import sys
from pathlib import Path
import pandas as pd

raw = Path(sys.argv[1] if len(sys.argv) > 1 else "data/raw")
files = sorted(raw.glob("*.csv"))
if not files:
    sys.exit(f"No CSV files in {raw}. See data/README.md.")

for f in files:
    df = pd.read_csv(f, low_memory=False)
    print(f"\n=== {f.name}: {len(df):,} rows x {df.shape[1]} cols ===")
    summary = pd.DataFrame({
        "dtype": df.dtypes.astype(str),
        "nulls": df.isna().sum(),
        "null_%": (df.isna().mean() * 100).round(1),
        "distinct": df.nunique(),
    })
    print(summary.to_string())
    date_cols = [c for c in df.columns if "date" in c or "timestamp" in c or c.endswith("_at")]
    for c in date_cols:
        s = pd.to_datetime(df[c], errors="coerce")
        print(f"  {c}: {s.min()} -> {s.max()}")
    for c in df.columns:
        if c.endswith("_id") and df[c].nunique() == len(df):
            print(f"  unique key candidate: {c}")
