[v1.1 · Premium] Spec 6.4 + 8.1. Prereq: P19.
1. Feature.Weather in Feature enum (free = No), crown badge + paywall.
2. IWeatherService using Open-Meteo (free, no API key): geocoding search + daily forecast (icon code, high/low). Privacy: user types a city name in Settings > General > Weather (NO automatic location, NO Windows Location API here). Only city lat/lon is sent, nothing else. Show this sentence in the settings page.
3. Cache forecast in SQLite for 3 h; works offline with last cached data (grey "updated 5h ago" tooltip). Unit toggle C/F with Windows region default.
4. Show small icon + high/low in Day and Week column headers and Agenda day headers (next 7-14 days only). Segoe Fluent icons, no emoji.
5. Failures are silent in UI (just no weather), logged without city name.
Tests: FakeHttp parse test, cache expiry, premium gating. Commit "feat: weather in headers".
