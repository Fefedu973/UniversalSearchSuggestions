using UniversalSearchSuggestions.Core.Browsers;
using UniversalSearchSuggestions.Core.Search;

namespace UniversalSearchSuggestions.Tests;

public sealed class BrowserBookmarkReaderTests
{
    [Fact]
    public void ReadChromiumBookmarksReturnsMatchingBookmarks()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("uss-bookmarks-");
        var bookmarksPath = Path.Combine(tempDirectory.FullName, "Bookmarks");
        File.WriteAllText(bookmarksPath, """
            {
              "roots": {
                "bookmark_bar": {
                  "children": [
                    { "type": "url", "name": "YouTube", "url": "https://www.youtube.com/" },
                    { "type": "url", "name": "Docs", "url": "https://learn.microsoft.com/" }
                  ]
                }
              }
            }
            """);

        try
        {
            var results = BrowserBookmarkReader.ReadChromiumBookmarks(bookmarksPath, "Chrome", "you", 5);

            Assert.Single(results);
            Assert.Equal("YouTube", results[0].Title);
            Assert.Equal(SuggestionSourceKind.BrowserBookmark, results[0].SourceKind);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
