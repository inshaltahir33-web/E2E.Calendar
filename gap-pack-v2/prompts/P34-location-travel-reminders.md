[v2] Spec 3.1. Prereq: P07 (reminder engine), P21 (travel block).
1. Location reminder: "When I get to office". Add Geolocator geofence (Windows.Devices.Geolocation.Geofencing, Entered/Exited, dwell time). Permission flow with a clear explanation screen BEFORE the OS prompt; if denied, the reminder falls back to a normal time reminder and says so.
2. Saved places: Home / Office / custom (name + lat/lon + radius 100-500 m) picked from a search box (Open-Meteo geocoding or a map-link paste). Data stays local.
3. Smart / travel-aware "Leave now": for events with a location, estimate travel time (routing API behind IRoutingService, default OSRM public demo or user-supplied key - make provider swappable) from current location, fire "Leave in X min" toast = event start - travel - buffer. Recalculate every 10 min when within 2 h, only while the event is upcoming.
4. Battery/privacy: no background location unless a location reminder is active; show a tray tooltip "Location in use" when active. Turn everything off with one switch in Settings > Privacy.
5. Never send location to any server other than the routing request and never log coordinates.
Tests: fake geofence events, fallback on permission denied, leave-time math. Commit "feat: location and travel-aware reminders".
