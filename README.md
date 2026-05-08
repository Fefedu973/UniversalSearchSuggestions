# Universal Search Suggestions

Command Palette extension for PowerToys that brings browser-like search suggestions into CmdPal:

- direct URL detection, so `youtube.com` opens as a URL instead of a search
- Google, Bing, DuckDuckGo and Brave autocomplete providers, plus optional legacy providers from the original PowerToys Run plugin: Yahoo, Ecosia, Qwant, Swisscows and Google Toolbar XML
- preconfigured opening engines from the original plugin: Google, Bing, Yahoo, Baidu, Yandex, DuckDuckGo, Naver, Ask, Ecosia, Brave, Qwant, Startpage, Swisscows, Dogpile, Gibiru, Mojeek, MetaGer, ZapMeta, Search Encrypt, OneSearch and Ekoru
- Google rich autocomplete parsing through `client=gws-wiz`, including descriptions and thumbnails when Google returns them
- Google `client=chrome` fallback for browser-style items such as calculator suggestions and navigation suggestions
- optional Google Omnibox answer source using Chrome's `client=chrome-omni` suggest flow for inline answers such as calculator and short factual answers when Google exposes them
- optional beta detail enrichment that loads Markdown asynchronously from free DuckDuckGo Instant Answer and Wikipedia endpoints
- optional beta AI detail answer using a configurable GET endpoint template; disabled by default because it sends the query to a third-party service
- cached Google S2 favicons for direct URLs, navigation suggestions, bookmarks and history, with a link icon fallback while loading or after failure
- optional local browser bookmarks and history from one selected Chrome, Edge, Brave or Firefox profile source
- configurable opening search engine, autocomplete sources, custom search URL, browser target, local browser source, result limits and detail panel

The extension deliberately does not use headless Google screenshots anymore. That approach is slow, fragile, and regularly blocked. The current design uses lightweight autocomplete endpoints and rich metadata where available, then leaves SERP previews as a future optional provider boundary for a real search API.

Search suggestion sources and the opening search engine are intentionally separate. For example, Bing can provide an autocomplete phrase while selecting that phrase still opens it with Google, DuckDuckGo, Brave, or the configured custom URL.

Google's Omnibox answers are parsed from autocomplete metadata, not from a rendered Google results page. This keeps the extension fast and avoids the fragile headless-browser screenshot path.

The beta rich details path is deliberately separate from suggestion fetching. Suggestions render first; details are cached and appended to the Markdown panel when the background request finishes.

## Development

Build:

```powershell
.\scripts\build.ps1 -Configuration Debug -Platform x64
```

Test:

```powershell
.\scripts\test.ps1 -Configuration Debug -Platform x64
```

Deploy the dev MSIX registration and ask Command Palette to reload extensions:

```powershell
.\scripts\dev-deploy.ps1 -Configuration Debug -Platform x64
```

The project targets `Microsoft.CommandPalette.Extensions` `0.9.260303001` and Windows SDK package `10.0.26100.38`.

## Architecture

`UniversalSearchSuggestions.Core` contains all testable behavior: URL heuristics, search URL construction, provider parsing, browser bookmark/history readers, local browser query parsing, favicon URL construction, image reference handling and result merging.

`UniversalSearchSuggestions` is now a thin Command Palette adapter: settings, list item rendering, detail Markdown and browser launch commands. The direct URL/search action is shown immediately on each keystroke; slower network and browser-local results arrive later. Search refreshes are versioned and cancellable, so stale async responses from older keystrokes cannot overwrite newer results.

`UniversalSearchSuggestions.Tests` covers the fragile parser and heuristic logic with focused unit tests.

## Notes

Local browser history is off by default because it can be large and privacy-sensitive. Bookmarks are enabled by default. Both options are available in the extension settings. Local results are limited to one selected browser source and indexed in memory, so keystrokes search the cache instead of copying/querying SQLite repeatedly. Firefox bookmarks are read from `places.sqlite`; Chromium bookmarks are read from the `Bookmarks` JSON file.

Local browser search supports Raycast-style terms: `foo bar -baz` requires `foo` and `bar` while excluding `baz`; `\-baz` searches the literal dash.

Command Palette `TextToSuggest` is exposed through the "Complétion dans le champ" setting. In current Command Palette builds this is applied with the right arrow key when an item is selected.

Custom search URLs support `{query}`, `{query+}` and `%s` placeholders.
