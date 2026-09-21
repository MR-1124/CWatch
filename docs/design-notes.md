# C:Watch design notes

Working notes from the v1.x visual redesign ("pressure gauge" system).
Read this before making UI changes so the system stays coherent.

## The one idea

C:Watch is a **storage instrument**. The dashboard hero is the capacity
gauge: a segmented bar with a 90% red line, filled in state color
(signal below 80%, caution to 90%, critical past it). Everything else
stays quiet. When adding UI, ask: does this belong on the instrument,
or is it decoration? Cut decoration.

## Tokens

| Token | Dark | Light | Use |
|---|---|---|---|
| BgCanvas | `#0C0E12` | `#F2F4F7` | Window canvas |
| BgCard / Secondary / Nested | `#151922` / `#1A1F2A` / `#11141C` | white / `#F8F9FB` / `#EEF1F5` | Panel hierarchy |
| BorderSubtle / Light / Hover | `#252B38` / `#333B4C` / `#4A566E` | `#D7DDE6` / `#C3CCDA` / `#8F9DB2` | Hairline → active |
| TextPrimary / Secondary / Muted | `#F2F4F7` / `#9AA6B5` / `#677182` | `#141B26` / `#4A5659`-ish slate / `#6E7A8C` | Text hierarchy |
| AccentOrange (signal) | `#F2632B` | `#C74814` | **Pressure and the primary action only** |

State colors: nominal `#3DB583`, velocity `#4CC3E0`, caution `#E3A93C`,
critical `#E0524A`, system `#518CE0`. Light-theme variants are deeper
(see `ThemeManager.ApplyTheme`) — never hard-code a hex in a view;
bind a `DynamicResource` or a theme-aware converter.

## Type

- Segoe UI Variable **Display** for numerals and page titles; **Text**
  for everything else. One icon face: Segoe Fluent Icons (MDL2 fallback).
- Scale: 10 / 11 / 12 / 13 / 15 / 18 / 26. Nothing outside it.
- Tabular figures everywhere (`Typography.NumeralAlignment` is global).
- Sentence case for every label and button. No ALL-CAPS labels.

## Structure

- Radius = hierarchy: cards 6, controls 4, chips 2. (Stored as literals
  today — a CornerRadius-typed resource broke BAML compilation at
  runtime; revisit only with a smoke test.)
- Safety is never color alone: state colors sit next to a colored rule
  or glyph shape.
- Focus is visible: single dashed `FocusRing` style on all interactive
  controls.

## Things already tried (do not re-add)

- ALL-CAPS micro-labels with colons ("SAFETY LEVEL: ") — replaced with
  plain sentence case.
- Emoji glyphs in nav and rows — replaced with MDL2 vectors.
- "Audit: pass" pill in Reports — decorative, removed.
- Stock Material orange `#FF5722` / Tailwind emerald `#10B981` — reads
  as template chrome.
- Big number + small label hero — the gauge replaced it.

## Verification habit

XAML compiles even when it will crash at parse time. Before calling UI
work done: `dotnet build`, full test suite, then launch the exe and
compare `ERROR` count in `%LOCALAPPDATA%\CWatch\logs\<today>.log`
before vs. after. A "pass" is zero new errors.
