<div align="center">

<img src="UniversalSearchSuggestions/Assets/logo.png" alt="Universal Search Suggestions" width="128" />

# Universal Search Suggestions

**Browser-grade search and AI answers, right in [Microsoft Command Palette](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview).**

[![Release](https://img.shields.io/github/v/release/Fefedu973/UniversalSearchSuggestions?style=for-the-badge&color=4F46E5&logo=github)](https://github.com/Fefedu973/UniversalSearchSuggestions/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Fefedu973/UniversalSearchSuggestions/total?style=for-the-badge&color=10B981&logo=github)](https://github.com/Fefedu973/UniversalSearchSuggestions/releases)
[![License](https://img.shields.io/github/license/Fefedu973/UniversalSearchSuggestions?style=for-the-badge&color=06B6D4)](LICENSE)
[![Stars](https://img.shields.io/github/stars/Fefedu973/UniversalSearchSuggestions?style=for-the-badge&color=F59E0B&logo=github)](https://github.com/Fefedu973/UniversalSearchSuggestions/stargazers)

[![Windows 11](https://img.shields.io/badge/Windows-11-0078D4?style=flat-square&logo=windows11)](https://www.microsoft.com/windows/windows-11)
[![PowerToys](https://img.shields.io/badge/PowerToys-Command%20Palette-9333EA?style=flat-square&logo=microsoft)](https://learn.microsoft.com/windows/powertoys/command-palette/overview)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![CmdPal Extensions](https://img.shields.io/badge/CmdPal-Extensions-2563EB?style=flat-square)](https://github.com/microsoft/CmdPal-Extensions)

[**Install**](#-install) · [**Features**](#-features) · [**Settings**](#-settings) · [**FAQ**](#-faq) · [**Contributing**](#-contributing)

</div>

---

## ✨ What it does

Type anything in **Command Palette** and get the same suggestions you'd get in your browser's address bar — autocomplete from Google / Bing / DuckDuckGo / Brave / Qwant / …, calculator and unit answers, weather snippets, dictionary entries, your local **bookmarks and history**, **direct URL detection** and **AI-powered answers** streamed live in the details panel.

It's not a screen-scraper. There's **no headless browser, no SERP screenshots, no fragile DOM parsing** — only the lightweight autocomplete endpoints browsers themselves use, plus a few free APIs (DuckDuckGo Instant Answer, Wikipedia REST) for the optional details panel.

```
┌──────────────────────────────────────────────────────────────────────┐
│  ⌨  capitale de la france                                            │
├──────────────────────────────────────────────────────────────────────┤
│  🔍  capitale de la france                       Search              │
│  💡  Paris                                       Google answer       │
│  ⭐  Capitale de la France — wikipedia.org       Bookmark — Firefox  │
│  🌐  capitale de la france meteo                 Google              │
│  🌐  capitale de la france carte                 Google              │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 🚀 Install

> Requires **Windows 11** with **PowerToys ≥ 0.93** (Command Palette enabled).

### Option A — From the [CmdPal Extensions gallery](https://github.com/microsoft/CmdPal-Extensions) *(recommended once published)*

Open **Command Palette → Install command palette extension** and pick `Universal Search Suggestions`.

### Option B — From the latest GitHub release

1. Download the latest `.msix` from [Releases](https://github.com/Fefedu973/UniversalSearchSuggestions/releases/latest)
2. Double-click and **Install**
3. Open Command Palette (default `Win`+`Alt`+`Space`) and start typing — the new extension page is auto-loaded

### Option C — Build from source

```powershell
git clone https://github.com/Fefedu973/UniversalSearchSuggestions
cd UniversalSearchSuggestions
.\scripts\dev-deploy.ps1 -Configuration Release -Platform x64
```

---

## 🧩 Features

### Suggestions everywhere you'd expect them

| Source | What you get | Default |
|---|---|---|
| 🟦 **Google** autocomplete | the same `gws-wiz` rich payload Chrome's address bar uses — text + descriptions + thumbnails | ✅ on |
| 🟦 **Google rich** | descriptions and miniatures (sport scores, finance tickers, dictionary…) when Google exposes them | ✅ on |
| 🟦 **Google Omnibox answers** | calculator, weather, currency, sunrise/sunset, translation, dictionary, local time, sports, finance — inline, no SERP needed | ✅ on |
| 🟦 **Google legacy XML** | the original Toolbar XML feed kept for compatibility | ⬜ off |
| 🟧 **Bing**, 🦆 **DuckDuckGo**, 🦁 **Brave**, 🌳 **Ecosia**, 🇫🇷 **Qwant**, 🇨🇭 **Swisscows**, 💜 **Yahoo** | their public autocomplete JSON arrays | toggleable |
| 📚 **Local bookmarks & history** | one Chrome / Edge / Brave / Firefox profile, scanned in-memory for fast typing | ✅ on |
| 🔗 **Direct URL detection** | `github.com/user/repo` opens directly — no detour through search | always on |

### A details panel that actually does something

When you select an item, the right-hand Markdown panel can show:

- 🌐 a **rich web summary** — DuckDuckGo Instant Answer first, then Wikipedia (with cached thumbnail!) — for known entities, definitions, snippets
- 🤖 an **AI-streamed answer** in real-time — works out of the box with the **anonymous, no-key OVH endpoint**, and can be pointed at any **OpenAI-compatible** chat completions endpoint (OpenAI, Groq, Together, OpenRouter, Mistral La Plateforme, Azure OpenAI, …) with an optional API key
- 🐛 a **debug section** showing AI delay / request / streaming / chunk count / errors when something looks off

> All thumbnails (Wikipedia, DuckDuckGo, Google rich) are **cached locally**, so AI streaming refreshes don't re-hammer remote servers.

### Open the way you want

Each suggestion exposes a context menu with extra commands:

- 🌐 open with the **default browser** (or any installed Chrome / Edge / Brave / Firefox / custom path)
- 👤 open with a **specific browser profile** (Chrome / Edge / Brave / Firefox)
- 🕵️ open in **Incognito / InPrivate / private window**
- 📋 **copy URL**

### Zero-typing mode

The empty palette can show:

- ⏱️ **recent searches** (kept locally — Google history requires a browser session)
- 🟦 **trending Google suggestions** of the day
- both, mixed and merged

### Local search syntax

Local bookmarks/history use Raycast-style operators:

```
gh -clone           # match "gh", exclude "clone"
"react hooks"       # exact phrase
\-deprecated        # literal dash, do not exclude
```

---

## ⚙️ Settings

All defaults below match a fresh install — they're tuned so the extension **works great immediately** with no tinkering.

| Setting | Default |
|---|---|
| Primary search engine | **Google** |
| Browser | **System default** |
| Local browser source | **Same as opening browser** |
| Custom search URL | `https://www.google.com/search?q={query}` |
| Language code | **auto-detected** from Windows UI |
| Google autocomplete | ✅ |
| Google rich + Omnibox answers | ✅ |
| Google Toolbar XML | ⬜ |
| Bing / Yahoo / DDG / Ecosia / Brave / Qwant / Swisscows | ⬜ |
| Bookmarks + history | ✅ |
| Details panel | ✅ |
| Rich web details (Wikipedia / DDG) | ✅ |
| AI answer in details | ✅ — anonymous OVH Llama 3.1 8B |
| Live details refresh | ✅ |
| AI endpoint | `https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions` |
| AI model | `Llama-3.1-8B-Instruct` |
| AI API key | empty — add one to use OpenAI / Groq / OpenRouter / … |
| AI debug | ⬜ |
| Empty palette content | **Recent searches + Google suggestions** |
| Search-box autocomplete | ✅ |
| Site favicons | ✅ |
| Bookmarks/history separator | ✅ |
| Decode base64 images | ✅ |
| Max suggestions per source | **10** |
| Max local results | **12** |
| Max total results | **40** |
| Network debounce | **110 ms** |

### Bring your own AI

Plug the **AI endpoint** + **AI API key** into any OpenAI-compatible service:

| Provider | Endpoint | Suggested model |
|---|---|---|
| OpenAI | `https://api.openai.com/v1/chat/completions` | `gpt-4o-mini` |
| Groq | `https://api.groq.com/openai/v1/chat/completions` | `llama-3.1-8b-instant` |
| OpenRouter | `https://openrouter.ai/api/v1/chat/completions` | `meta-llama/llama-3.1-8b-instruct` |
| Together AI | `https://api.together.xyz/v1/chat/completions` | `meta-llama/Llama-3.1-8B-Instruct-Turbo` |
| Mistral | `https://api.mistral.ai/v1/chat/completions` | `mistral-small-latest` |
| OVH (default, anonymous) | `https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions` | `Llama-3.1-8B-Instruct` |

Empty key → falls back to the anonymous OVH endpoint (rate-limited but free, no signup).

---

## ❓ FAQ

<details>
<summary><b>Why so many Google sources?</b></summary>

They use different Google APIs and complement each other:

- **Google autocomplete** is the regular text suggestion list.
- **Google rich** decodes the `zh` / `zi` / `zs` keys exposed by `client=gws-wiz` (rich heading, description, thumbnail).
- **Google Omnibox answers** is the structured answer feed Chrome uses for calculator, weather, finance, currency, dictionary, time, sports — all the inline things you see when typing in Chrome's address bar.
- **Google Toolbar XML** is the legacy 2008-era XML autocomplete API — kept off by default, useful only if the modern endpoints are blocked on your network.

In practice, leaving rich + Omnibox on (defaults) gives you the full Chrome address-bar experience.
</details>

<details>
<summary><b>Is the AI answer sending my queries to a server?</b></summary>

Yes — when "AI answer in details" is enabled, the typed query is sent to whichever endpoint is configured. The default endpoint is OVH's anonymous Llama 3.1 inference; no account, no key, no logging documented. You can switch it off, change the endpoint, or add an API key for a different provider at any time.
</details>

<details>
<summary><b>Why isn't Google's "trending searches before I type" hitting my account?</b></summary>

Google ties trending suggestions to your signed-in browser session through cookies. A standalone process (us) cannot replay that session, so the recent-searches list is stored **locally** by the extension instead. Recent local queries, plus the anonymous "Google default suggestions" feed, are what the empty palette shows.
</details>

<details>
<summary><b>Will this slow down Command Palette?</b></summary>

No. Suggestions render immediately for the typed query (the search action is local), and network sources are debounced (110 ms) and cancelled per keystroke. The details panel is fully async — it appears only when there's content to show, and never blocks the list. Wikipedia / DuckDuckGo thumbnails are cached on disk and reused across refreshes, so AI streaming doesn't re-fetch them.
</details>

<details>
<summary><b>Local bookmarks/history — what's the privacy story?</b></summary>

Nothing leaves your machine. The extension reads `Bookmarks` (JSON) and `History` / `places.sqlite` directly from one selected browser profile, on demand, into memory, and the index is filtered in-process. No telemetry. The cache directory is `%LOCALAPPDATA%\UniversalSearchSuggestions\`.
</details>

---

## 🛠️ For developers

### Build & test

```powershell
.\scripts\build.ps1   # build all 3 projects
.\scripts\test.ps1    # run xUnit suite
.\scripts\dev-deploy.ps1   # register MSIX and ask CmdPal to reload
```

Stack: **.NET 9 / C# 13**, **WindowsAppSDK 1.6**, **Microsoft.CommandPalette.Extensions 0.9**, **xUnit 2.9**.

### Project layout

| Project | Role |
|---|---|
| `UniversalSearchSuggestions.Core` | URL heuristics, suggestion providers, parsers, bookmark/history readers, image cache, localization. **Pure .NET, fully unit-tested.** |
| `UniversalSearchSuggestions` | Thin CmdPal adapter — settings UI, list-item rendering, details panel, browser launch. WinRT/MSIX. |
| `UniversalSearchSuggestions.Tests` | xUnit tests for the parser/heuristic logic. |

### Architecture in one paragraph

A typed query schedules an in-memory immediate result (the "Search …" action) plus parallel network calls to enabled providers. Responses are merged by query key, sorted by score, deduplicated, and ranked. The details panel for the current query item kicks off two more independent tasks — a web summary (DuckDuckGo → Wikipedia fallback) and an OpenAI-compatible streaming AI call — both throttled and refreshed via `INotifyPropChanged` on the `IDetails` body, so the list itself only re-renders once when the panel transitions from empty to populated.

---

## 🤝 Contributing

Issues, feature ideas and PRs are very welcome. A few guidelines:

- prefer adding a **new provider** in `UniversalSearchSuggestions.Core/Search/Providers/` and a unit test in `UniversalSearchSuggestions.Tests/`
- localized strings live in `UniversalSearchSuggestions.Core/Resources/Strings.resx` (English) and `Strings.fr.resx` (French) — please keep both in sync
- run `dotnet format` before committing

---

## 📄 License

[MIT](LICENSE) — do whatever, just keep the credit.

---

<div align="center">

Built with ❤ in France · Inspired by the original [PowerToys-Run-Universal-Search-Suggestions-Plugin](https://github.com/Fefedu973/PowerToys-Run-Universal-Search-Suggestions-Plugin) for the legacy PowerToys Run.

If this saves you keystrokes, [⭐ a star helps](https://github.com/Fefedu973/UniversalSearchSuggestions).

</div>
