// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace UniversalSearchSuggestions;

[Guid("3fadba00-c46f-4afb-b25c-ede7415287b7")]
public sealed partial class UniversalSearchSuggestions : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;

    private readonly UniversalSearchSuggestionsCommandsProvider _provider = new();

    public UniversalSearchSuggestions(ManualResetEvent extensionDisposedEvent)
    {
        this._extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };
    }

    public void Dispose()
    {
        _provider.Dispose();
        this._extensionDisposedEvent.Set();
    }
}
