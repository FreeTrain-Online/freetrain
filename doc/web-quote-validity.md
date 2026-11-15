# Quotes during a running simulation

Train trading and deployment, railway infrastructure, slopes, station buildings,
platforms and lanes, passages, city buildings, terrain editing and object removal
use a 720-minute quote window. A quote is valid at age 720 and expires at age 721,
measured in simulation minutes rather than wall-clock time. Pausing does not age it.

An issued preview can survive unrelated session and geometry revisions. The UI
keeps it available as time advances. Confirmation rebuilds the preview under the
session lock and compares it with the original server-issued review. Prices,
affected cells, route patterns, protected cars, losses and target identities must
still agree. A changed site or price requires another preview; confirmation never
silently substitutes the new result. Repeated preview requests with the same query
do not replace the original review or extend its lifetime.

World replacement, content changes, authoring mode changes, future timestamps,
future revisions, target revision conflicts and permission checks remain enforced.
A successfully confirmed quote cannot be reused with a new command request ID;
the existing receipt mechanism still deduplicates the original request ID.

`SessionHost.QuoteValidityMinutes` and `web/src/lib/quote-validity.ts` define the
window. Railway, slope, terrain and removal queries add an optional
`expectedMinute`; new web requests always supply it. Older requests without it
receive a server-timed lifetime when previewed. Commands without a retained
server-issued preview retain exact global revision checks for compatibility with
existing direct command callers.

The server keeps at most 4,096 query fingerprints and two million characters of
query keys, with each individual key bounded to 16 Ki characters. It keeps no
candidate worlds and writes no quote state into saves.
Restart or eviction can require another preview. Property editors and world
creation retain their existing conflict rules.

Regression coverage includes nine construction/trading action types at the
720/721-minute boundary, unrelated geometry changes, altered removal footprints,
replay, future requests, and browser purchases and station placement during Run.
