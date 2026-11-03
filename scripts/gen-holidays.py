#!/usr/bin/env python3
"""Generate bundled holiday calendars as ICS files.

Run from the repository root:
    python scripts/gen-holidays.py
"""

from __future__ import annotations

import re
from datetime import date, timedelta
from pathlib import Path

import holidays

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "src" / "E2E.Calendar.App" / "Resources" / "Holidays"

COUNTRIES = {
    "pk": ("PK", "Pakistan"),
    "us": ("US", "United States"),
    "uk": ("GB", "United Kingdom"),
    "ae": ("AE", "United Arab Emirates"),
    "in": ("IN", "India"),
}

ISLAMIC_MARKERS = (
    "eid",
    "ramadan",
    "ashura",
    "mawlid",
    "muharram",
    "hijri",
    "islamic",
    "youm-e",
)


def slugify(value: str) -> str:
    value = value.lower()
    value = re.sub(r"[^a-z0-9]+", "-", value)
    return value.strip("-") or "holiday"


def is_islamic(name: str) -> bool:
    lowered = name.lower()
    return any(marker in lowered for marker in ISLAMIC_MARKERS)


def build_ics(country_code: str, country_name: str) -> str:
    lines = [
        "BEGIN:VCALENDAR",
        "VERSION:2.0",
        "PRODID:-//BindLabs//Calendar Pro//EN",
        "CALSCALE:GREGORIAN",
        "METHOD:PUBLISH",
    ]

    all_holidays = holidays.country_holidays(country_code, years=range(2026, 2031))

    for holiday_date in sorted(all_holidays):
        raw_name = all_holidays[holiday_date]
        names = [part.strip() for part in str(raw_name).split(";") if part.strip()]
        for name in names:
            summary = name
            if is_islamic(name) and "(approx.)" not in summary:
                summary = f"{summary} (approx.)"
            uid = f"{country_code.lower()}-{holiday_date.isoformat()}-{slugify(name)}"
            end_date = holiday_date + timedelta(days=1)
            lines.extend(
                [
                    "BEGIN:VEVENT",
                    f"UID:{uid}",
                    f"DTSTAMP:{date.today().strftime('%Y%m%d')}T000000Z",
                    f"DTSTART;VALUE=DATE:{holiday_date.strftime('%Y%m%d')}",
                    f"DTEND;VALUE=DATE:{end_date.strftime('%Y%m%d')}",
                    f"SUMMARY:{summary}",
                    "END:VEVENT",
                ]
            )

    lines.append("END:VCALENDAR")
    return "\r\n".join(lines) + "\r\n"


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for file_stem, (country_code, country_name) in COUNTRIES.items():
        content = build_ics(country_code, country_name)
        target = OUT_DIR / f"{file_stem}.ics"
        target.write_text(content, encoding="utf-8", newline="")
        print(f"Wrote {target} ({content.count('BEGIN:VEVENT')} events)")


if __name__ == "__main__":
    main()
