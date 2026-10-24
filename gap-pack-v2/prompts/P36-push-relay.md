[v1.1/v2] Spec 5 (near-real-time updates). Prereq: P13, P14. Needs a small server.
Create docs/12-RELAY.md first (design + privacy). Goal: faster updates than 5-min polling WITHOUT the relay ever seeing calendar content.
1. Relay service (ASP.NET Core minimal API, container-ready, deploy target Azure Container Apps or Fly.io; keep config in env vars): 
   - POST /hooks/google (X-Goog-Channel-ID / X-Goog-Resource-State headers) and POST /hooks/graph (validationToken handshake + clientState check). 
   - Stores ONLY: opaque channel/subscription id -> device id + expiry. No event data, no tokens.
   - Forwards a tiny "changed" ping to the device through WNS (Windows Push Notification Service raw notification) using the app's PFN/channel URI.
2. Client: register WNS push channel, create Google `events.watch` channels and Graph `/subscriptions` (calendar events, <= 4230 min lifetime) pointing to the relay, renew before expiry (background timer + on resume), delete on account removal.
3. On ping: trigger an immediate incremental sync of that account. Polling stays as fallback and also runs if the relay is down/disabled. Settings > Privacy toggle "Instant updates (uses relay)" with an honest explanation.
4. Security: HTTPS only, validate Graph clientState and Google channel token (random per subscription), rate limit, no PII in logs.
Tests: webhook handshake/validation tests, renewal scheduler with fake clock, ping -> sync trigger. Commit "feat: push relay for instant sync".
