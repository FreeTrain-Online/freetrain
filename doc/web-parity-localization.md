# Localization structure — P2, 27 October 2026

The web client now separates application text, formatting rules and pack-owned
content translations. English and Japanese use the same registration path as a
future language. English defines the message-key shape for TypeScript; the
Japanese stable ExAV source remains the authority for gameplay meaning.

```text
web/src/locales/
  en/{app,gallery,formats}.json
  ja/{app,gallery,formats}.json
web/src/lib/
  localization.ts          # typed language registry, negotiation, interpolation
  locale-matching.ts      # shared BCP 47 parent lookup
  content-localization.ts # pack/source-bound presentation lookup
web/src/LanguageSelect.svelte
content/localization/exav-stable-bundled/
  source.json             # source identity, Japanese text/hash, donor and comparison
  en.json                 # reviewed English wording and review status
web/scripts/check-locales.ts
```

## Adding a language

1. Add a directory named with its canonical BCP 47 tag, for example `sv`,
   `pt-BR` or `zh-Hant`. Copy and translate `app.json` and `gallery.json`.
   Preserve keys and named parameters; translate complete messages. Keep names,
   credits, identifiers and user-authored station/train names intact.
2. Supply `formats.json`: seven weekdays starting on Sunday, a `gameTime`
   template and `frameCount` plural forms. Use the plural categories needed by
   that language (`zero`, `one`, `two`, `few`, `many`, `other`); `other` is required.
   Parameters may move within a sentence. For example Swedish can use
   `"gameTime": "År {year} · {day}/{month} ({weekday}) {time}"`.
3. Import those three files and add one entry to `locales` in
   `web/src/lib/localization.ts`, with its native display label and `ltr`/`rtl`
   direction. TypeScript derives `Locale` from this registry. Both language
   selectors, browser-language matching, HTML language/direction and the gallery
   consume the registry; do not add language branches to individual components.
4. Run `deno task --cwd web check:locales`, `test` and `build`. Check real layout,
   keyboard/focus, fonts and narrow screens for the new language. Direction metadata
   is implemented; a newly registered RTL language still needs layout acceptance.

Each UI locale must have all keys, nonempty messages and the same interpolation
parameters as English. New messages therefore expose missing translations during
builds. Additional domains can follow the gallery's separate file/typed registry
pattern rather than growing an all-language source file. The current small
catalogs are bundled eagerly for synchronous language switching; no remote
translation service or runtime dependency is required. Per-language lazy loading
can be added behind this registry if translation download size warrants it.

`interpolate` substitutes plain text once. It never evaluates code or HTML.
`pluralMessage` uses `Intl.PluralRules` and `Intl.NumberFormat`; it does not assume
that every language has English's singular/plural pair. Use `Intl` with the selected
locale for ordinary numeric and wall-clock presentation. The game clock retains
ExAV's fixed 365-day years, no leap days, source weekday sequence, arbitrary year
range and ten-minute display precision. Its localized template must never convert
simulation minutes through the host calendar or timezone. Raw source coordinates,
IDs and recipe fields in the developer gallery remain technical source evidence.

## Language preference and fallback

An available saved preference wins; otherwise the first supported entry in
`navigator.languages` wins, then English. Tags are canonicalized and matched
case-insensitively, trying the exact tag and progressively less specific parents.
For example `ja-JP` can use `ja`, and `zh-Hant-TW` can use `zh-Hant`; `pt-PT` does
not infer `pt-BR`. Invalid/unsupported preferences cannot block startup. Only the
locale ID is stored, with the existing safe preference key. Both the game and
review gallery set the document language/direction and persist explicit choices.

Optional credits use the same parent lookup, then the enabled set's own default
language. Their existing English/Japanese minimum is an acceptance policy, not a
closed list of supported languages. Operators can include more `<locale lang>`
entries in their own `plugin-set.xml`; original notices remain unchanged. Optional
credits and future content translation files stay in the optional distribution,
never in this repository's bundled catalogs or application locale files.

## Content text and review evidence

Shell text and content names have different owners. Never translate a plugin
namespace, contribution ID, recipe parameter or save/world identity. Bind a content
translation to pack ID + plugin ID + contribution ID + field. `source.json` records
the ExAV revision, exact manifest path/key and SHA-256, original Japanese text/hash,
English donor where present and the inventory's comparison classification.
`en.json` stores wording and `reviewed-with-feature` or `draft` status under that
identity. Japanese comes directly from the retained source text; it is not a
second manually synchronized copy.

For another content language, add a sibling locale JSON and register it in
`bundledLocalization.locales`. A content locale may be partial: an absent/draft
entry falls back to the source text. A changed pack, source revision or original
text also falls back. Build checks bind each source entry to the maintained
translation inventory and reject stale manifest/text hashes, unknown identities,
empty strings or changed parameters. A source update requires a wording review;
do not just refresh hashes to make a translation pass. Exact carryovers, changed
meanings and newly translated strings retain their donor/comparison records.

The delivered P2 content ledger covers the three music and six departure-bell
names used in the new audio controls. It is **not** an approval of all 977 bundled
XML candidates or later gameplay names/help. Those continue with their implementing
P3–P9 slices and final P11 reconciliation. Source authors and notices remain literal
attribution. Follow the existing terminology review in
[the content specification](web-parity-content.md#translation-and-implementation-status)
and `doc/parity/exav-content-rules.json` when adding feature vocabulary.

## Verification

Builds now run `check:locales`. Client tests cover missing/unknown keys, parameters,
regional/script fallback, ordered browser preferences, plural categories beyond
English, source drift, distribution boundaries and draft fallback. Browser tests
cover English/Japanese audio controls, a 360-pixel Japanese screen, gallery language
switch/reload, preserved canvas/selection and localized optional credits. Adding
another language requires its own linguistic/layout review; a matching key set
alone is not translation acceptance.
