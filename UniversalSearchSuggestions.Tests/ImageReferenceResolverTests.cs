using UniversalSearchSuggestions.Core.Utilities;

namespace UniversalSearchSuggestions.Tests;

public sealed class ImageReferenceResolverTests
{
    [Fact]
    public void ResolvePreservesHttpsImages()
    {
        var resolved = ImageReferenceResolver.Resolve("https://example.com/image.png", "unused", decodeDataImages: true);

        Assert.Equal("https://example.com/image.png", resolved);
    }

    [Fact]
    public void ResolveDecodesDataImagesToFileUri()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("uss-images-");
        const string transparentPixel = "data:image/png;base64,iVBORw0KGgo=";

        try
        {
            var resolved = ImageReferenceResolver.Resolve(transparentPixel, tempDirectory.FullName, decodeDataImages: true);

            Assert.NotNull(resolved);
            Assert.StartsWith("file:///", resolved, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(new Uri(resolved!).LocalPath));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
