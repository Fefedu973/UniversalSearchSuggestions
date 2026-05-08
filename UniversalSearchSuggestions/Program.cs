// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;

namespace UniversalSearchSuggestions;

public class Program
{
    [MTAThread]
    public static async Task Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            await using global::Shmuelie.WinRTServer.ComServer server = new();
            ManualResetEvent extensionDisposedEvent = new(false);
            UniversalSearchSuggestions extensionInstance = new(extensionDisposedEvent);
            server.RegisterClass<UniversalSearchSuggestions, IExtension>(() => extensionInstance);
            server.Start();
            extensionDisposedEvent.WaitOne();
        }
    }
}
