<div align="center">

<img src="images/logo.png" alt="Universal Search Suggestions" width="128" />

# Universal Search Suggestions

**Your browser's address bar — inside the [Windows Command Palette](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview).**

Type a query, get the same live suggestions, instant answers, bookmarks and AI replies you'd see in Chrome's omnibox — without ever opening a browser.

[![Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Get%20it-0078D4?style=flat&logo=microsoft&logoColor=white)](https://apps.microsoft.com/detail/9nccznpqb9s6)
[![Release](https://img.shields.io/github/v/release/Fefedu973/UniversalSearchSuggestions?style=flat&color=4F46E5&logo=github&label=Release)](https://github.com/Fefedu973/UniversalSearchSuggestions/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Fefedu973/UniversalSearchSuggestions/total?style=flat&color=10B981&logo=github&label=Downloads)](https://github.com/Fefedu973/UniversalSearchSuggestions/releases)
[![Stars](https://img.shields.io/github/stars/Fefedu973/UniversalSearchSuggestions?style=flat&color=F59E0B&logo=github&label=Stars)](https://github.com/Fefedu973/UniversalSearchSuggestions/stargazers)
[![License](https://img.shields.io/github/license/Fefedu973/UniversalSearchSuggestions?style=flat&color=06B6D4&label=License)](LICENSE)
[![Mentioned in Awesome PowerToys Run](https://awesome.re/mentioned-badge.svg)](https://github.com/hlaueriksson/awesome-powertoys-run-plugins)

[![Windows 11](https://img.shields.io/badge/Windows-11-0078D4?style=flat&logo=windows11)](https://www.microsoft.com/windows/windows-11)
[![PowerToys](https://img.shields.io/badge/PowerToys-Command%20Palette-9333EA?style=flat&logo=microsoft)](https://learn.microsoft.com/windows/powertoys/command-palette/overview)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)

[**Install**](#install) · [**What you get**](#what-you-get) · [**Settings**](#settings) · [**FAQ**](#faq) · [**Contribute**](#contribute)

</div>

<div align="center">

https://github.com/Fefedu973/UniversalSearchSuggestions/raw/master/images/demo.mp4

<sub><i>30‑second demo — autocomplete, instant answers, local bookmarks, AI panel.</i></sub>

</div>

---

## What it is

Open the Command Palette (`Win` + `Alt` + `Space`), start typing, and Universal Search Suggestions reacts the way a browser would:

- live autocomplete from **Google, Bing, DuckDuckGo, Brave, Yahoo, Ecosia, Qwant, Swisscows**
- inline **answers** for math, weather, currencies, definitions, sports scores, sunrise / sunset, translations, local time
- your own **bookmarks and history** from Chrome, Edge, Brave, or Firefox
- a **rich details panel** with a Wikipedia summary and a streamed **AI answer**
- direct URL detection (`github.com/user/repo` → opens straight to the page)

Pick a result, hit Enter, and it opens in your browser. That's it.

---

## Install

> Requires **Windows 11** with **PowerToys 0.95+** and the **Command Palette** module enabled.

### From the Command Palette gallery (recommended)

In the Command Palette, run **Install command palette extension**, then pick **Universal Search Suggestions**.

### From the Microsoft Store

Single click install with automatic updates:

<a href="https://get.microsoft.com/installer/download/9NCCZNPQB9S6?referrer=appbadge" target="_self">
  <img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200" alt="Get it from Microsoft" />
</a>

### From WinGet

```powershell
winget install Fefedu973.UniversalSearchSuggestions
```

Same package as the Microsoft Store, installed from the command line. WinGet handles updates automatically.

### From a GitHub release

1. Open the [latest release](https://github.com/Fefedu973/UniversalSearchSuggestions/releases/latest).
2. Download the `.msix` matching your CPU — `_x64.msix` for Intel/AMD, `_arm64.msix` for ARM.
3. Double-click it and choose **Install**.
4. Open the Command Palette — the new page is loaded automatically.

### From source

```powershell
git clone https://github.com/Fefedu973/UniversalSearchSuggestions
cd UniversalSearchSuggestions
.\scripts\dev-deploy.ps1 -Configuration Release -Platform x64
```

---

## What you get

### Search suggestions from every major engine

| Source | Notes |
|---|---|
| **Google** | The same fast autocomplete used by Chrome's omnibox |
| **Google rich** | Adds descriptions and thumbnails (sports, finance, dictionary…) when available |
| **Google answers** | Calculator, weather, currency conversion, dictionary, sunrise/sunset, translation, local time, sports, finance — shown inline, no extra click |
| **Bing, DuckDuckGo, Brave, Yahoo, Ecosia, Qwant, Swisscows** | All optional, all free, all using the same public autocomplete endpoints their own websites use |
| **Direct URL detection** | Type `youtube.com/watch?v=…` and it opens directly — no detour through search |

Google is on by default. Anything else is one toggle away.

<table>
<tr>
<td width="50%"><img src="images/suggestions.png" alt="Live autocomplete suggestions" /></td>
<td width="50%"><img src="images/link%20detection.png" alt="Direct URL detection" /></td>
</tr>
<tr>
<td align="center"><sub>Live autocomplete as you type.</sub></td>
<td align="center"><sub>Type a URL — it opens straight to the page.</sub></td>
</tr>
</table>

### Inline answers like Chrome's omnibox

Math, currency, units, definitions, translations, stock quotes, general knowledge — answered inline, no extra click.

<table>
<tr>
<td width="33%"><img src="images/calculator.png" alt="Calculator" /></td>
<td width="33%"><img src="images/currency.png" alt="Currency conversion" /></td>
<td width="33%"><img src="images/conversion.png" alt="Unit conversion" /></td>
</tr>
<tr>
<td align="center"><sub>Calculator</sub></td>
<td align="center"><sub>Currency</sub></td>
<td align="center"><sub>Units</sub></td>
</tr>
<tr>
<td><img src="images/definition.png" alt="Dictionary definition" /></td>
<td><img src="images/translation.png" alt="Translation" /></td>
<td><img src="images/stock.png" alt="Stock quote" /></td>
</tr>
<tr>
<td align="center"><sub>Dictionary</sub></td>
<td align="center"><sub>Translation</sub></td>
<td align="center"><sub>Stock quotes</sub></td>
</tr>
<tr>
<td colspan="3"><img src="images/general%20knowledge.png" alt="General knowledge" /></td>
</tr>
<tr>
<td align="center" colspan="3"><sub>General knowledge — facts, sport scores, sunrise/sunset, local time, and more.</sub></td>
</tr>
</table>

### Your local bookmarks and history

Pick **one** browser profile (Chrome, Edge, Brave, or Firefox) and Universal Search will quietly index its bookmarks and history, ready to be matched as you type. Everything stays on your machine.

You can use Raycast-style operators in the search box:

```
gh -clone           match "gh", exclude "clone"
"react hooks"       exact phrase
\-deprecated        treat the dash as literal
```

<div align="center">
<img src="images/local%20history%20-%20bookmarks.png" alt="Local bookmarks and history mixed into the suggestions" width="800" />
<br /><sub>Your own bookmarks and history surface alongside live web suggestions.</sub>
</div>

### A details panel that helps you decide

Select a suggestion and the right side shows what the result is *about* — without opening it:

- **Web summary** from DuckDuckGo Instant Answer or Wikipedia, with a thumbnail
- **AI answer** streamed live in Markdown, in your language
- Or connect a SERP API ato get better results (coming soon)

The default AI runs on a free, no-account, anonymous endpoint (OVH's hosted Llama 3.1). Bring your own API key to point it at OpenAI, Groq, OpenRouter, Together, Mistral, Azure OpenAI — or any OpenAI-compatible service.

<table>
<tr>
<td width="50%"><img src="images/rich%20wikipedia.png" alt="Rich Wikipedia summary" /></td>
<td width="50%"><img src="images/ai%20overview.png" alt="AI answer streamed live" /></td>
</tr>
<tr>
<td align="center"><sub>Web summary from Wikipedia / DuckDuckGo with thumbnail.</sub></td>
<td align="center"><sub>AI answer streamed live in your language.</sub></td>
</tr>
</table>

### Open it the way you want

Every suggestion has a context menu with:

- Open in your **default browser** or any installed one (Chrome, Edge, Brave, Firefox, custom path)
- Open in a **specific browser profile**
- Open in **Incognito / InPrivate / private** mode
- **Copy URL**

<div align="center">
<img src="images/advanced%20actions.png" alt="Per-suggestion context menu with browser, profile and incognito options" width="800" />
<br /><sub>Right-arrow on any suggestion to pick a specific browser, profile, or incognito mode.</sub>
</div>

### A useful empty state

Before you've typed anything, the page can show:

- your **recent searches** (stored locally)
- the day's **trending Google suggestions**
- both, blended

<div align="center">
<img src="images/default%20suggestions.png" alt="The empty state with default suggestions before you've typed anything" width="800" />
<br /><sub>What you see the moment you open the page — recent and trending, blended.</sub>
</div>

---

## Settings

Universal Search ships with sensible defaults — installing it and starting to type is enough. Everything below can be tuned later from the extension's settings page.

| Setting | Default |
|---|---|
| Primary search engine | Google |
| Browser | System default |
| Suggestion language | Auto-detected from Windows |
| Google autocomplete + rich + answers | On |
| Bing, Yahoo, DuckDuckGo, Ecosia, Brave, Qwant, Swisscows | Off |
| Bookmarks + history | On (from your default browser) |
| Details panel | On |
| Web summary (Wikipedia + DuckDuckGo) | On |
| AI answer in details | On — anonymous OVH Llama 3.1 8B |
| Empty page content | Recent searches + Google suggestions |
| Site favicons | On |
| Max suggestions per source | 10 |
| Max local results | 12 |
| Max total results | 40 |
| Network debounce | 110 ms |

### Use your own AI provider

Paste a different endpoint and key into the AI settings and you're done:

| Provider | Endpoint | Suggested model |
|---|---|---|
| OpenAI | `https://api.openai.com/v1/chat/completions` | `gpt-4o-mini` |
| Groq | `https://api.groq.com/openai/v1/chat/completions` | `llama-3.1-8b-instant` |
| OpenRouter | `https://openrouter.ai/api/v1/chat/completions` | `meta-llama/llama-3.1-8b-instruct` |
| Together AI | `https://api.together.xyz/v1/chat/completions` | `meta-llama/Llama-3.1-8B-Instruct-Turbo` |
| Mistral | `https://api.mistral.ai/v1/chat/completions` | `mistral-small-latest` |
| OVH (default, no key) | `https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions` | `Llama-3.1-8B-Instruct` |

Leave the API key empty and you fall back to the anonymous OVH endpoint — free, rate-limited, no signup.

---

## FAQ

<details>
<summary><b>Is anything sent to a server?</b></summary>

The autocomplete sources you turn on receive your typed query the same way they would in a browser address bar. Bookmarks and history never leave your computer. The AI answer (if enabled) goes to the endpoint configured in settings — by default, OVH's anonymous Llama 3.1 endpoint. You can disable it, change the endpoint, or add your own API key at any time.

</details>

<details>
<summary><b>Will it slow the Command Palette down?</b></summary>

No. The "Search…" row appears the moment you type — that one is local. Network sources are debounced (110 ms by default) and cancelled on every keystroke. The details panel loads asynchronously and never blocks the list. Wikipedia and DuckDuckGo thumbnails are cached on disk, so re-selecting a result is instant.

</details>

<details>
<summary><b>Why are there several "Google" toggles?</b></summary>

Google exposes a few different autocomplete feeds and each adds something different:

- **Google autocomplete** is the regular suggestion list.
- **Google rich** adds descriptions, headings, and thumbnails (definitions, sport scores, stocks, etc.).
- **Google answers** adds inline calculator, weather, currency, dictionary, time and translation results — the things you see in Chrome's omnibox.
- **Google Toolbar XML** is the legacy 2008-era feed, off by default. Turn it on only if the modern endpoints don't work on your network.

In normal use, the three default-on Google sources together give you the full Chrome address-bar feel.

</details>

<details>
<summary><b>Where are bookmarks and history read from?</b></summary>

From the browser profile you picked in **Bookmarks/history browser**. Chromium browsers expose them through a `Bookmarks` JSON file and a `History` SQLite database; Firefox uses `places.sqlite`. Universal Search reads them on demand into memory and matches them locally — no telemetry, no sync, no network call. The local cache lives at `%LOCALAPPDATA%\UniversalSearchSuggestions\`.

</details>

<details>
<summary><b>Why don't I see my Google search history before I type?</b></summary>

Google ties personalized "recent searches" to your signed-in browser session via cookies. A standalone process can't reuse that session, so Universal Search keeps its own local list of recent queries instead. You can also display Google's anonymous trending suggestions, or both.

</details>

<details>
<summary><b>How do I clear the cache?</b></summary>

Open the Command Palette, find the Universal Search top-level command, press the right arrow to open its context menu, and pick **Reset Universal Search cache**. This clears favicons, thumbnails, AI responses, and recent searches.

</details>

---

## Contribute

Issues, ideas, and pull requests are very welcome.

- **Add a new search source** in [`UniversalSearchSuggestions.Core/Search/Providers/`](UniversalSearchSuggestions.Core/Search/Providers/) and a unit test next to it.
- **Add or update translations** in [`Strings.resx`](UniversalSearchSuggestions.Core/Resources/Strings.resx) (English) and [`Strings.fr.resx`](UniversalSearchSuggestions.Core/Resources/Strings.fr.resx) (French) — please keep both in sync.
- Run `dotnet format` before committing.

### Build and test

```powershell
.\scripts\build.ps1        # build all projects
.\scripts\test.ps1         # run the xUnit suite
.\scripts\dev-deploy.ps1   # register the MSIX and reload Command Palette
```

Stack: **.NET 9 / C# 13**, **WindowsAppSDK 1.6**, **Microsoft.CommandPalette.Extensions 0.9**, **xUnit 2.9**.

### Project layout

| Project | Role |
|---|---|
| [`UniversalSearchSuggestions.Core`](UniversalSearchSuggestions.Core/) | Pure .NET library: providers, parsers, URL heuristics, bookmark/history readers, image cache, localization. Fully unit-tested. |
| [`UniversalSearchSuggestions`](UniversalSearchSuggestions/) | The Command Palette extension itself: settings UI, list rendering, details panel, browser launching. |
| [`UniversalSearchSuggestions.Tests`](UniversalSearchSuggestions.Tests/) | xUnit tests for the parsing and ranking logic. |

---

## License

[MIT](LICENSE) — use it anywhere, just keep the credit.

---

<div align="center">

Made with care, inspired by the original [PowerToys Run plugin](https://github.com/Fefedu973/PowerToys-Run-Universal-Search-Suggestions-Plugin).

If it saves you keystrokes, [a star helps](https://github.com/Fefedu973/UniversalSearchSuggestions). ⭐

</div>
