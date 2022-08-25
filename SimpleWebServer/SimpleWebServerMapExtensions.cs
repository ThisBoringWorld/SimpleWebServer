using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Microsoft.AspNetCore.SimpleServer;

public static class SimpleWebServerMapExtensions
{
    public static SimpleWebServer MapGet<TResult>(this SimpleWebServer server, string path, Func<SimpleHttpContext, TResult> requestDelegate)
        => server.Map(HttpMethod.Get, path, context => ResponseAsApplicationJson(context, requestDelegate(context)));

    public static SimpleWebServer MapGet<TResult>(this SimpleWebServer server, string path, Func<SimpleHttpContext, Task<TResult>> requestDelegate)
        => server.Map(HttpMethod.Get, path, async context => await ResponseAsApplicationJson(context, await requestDelegate(context)));

    public static SimpleWebServer MapPost<TResult>(this SimpleWebServer server, string path, Func<SimpleHttpContext, TResult> requestDelegate)
        => server.Map(HttpMethod.Post, path, context => ResponseAsApplicationJson(context, requestDelegate(context)));

    public static SimpleWebServer MapPost<TResult>(this SimpleWebServer server, string path, Func<SimpleHttpContext, Task<TResult>> requestDelegate)
        => server.Map(HttpMethod.Post, path, async context => await ResponseAsApplicationJson(context, await requestDelegate(context)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task ResponseAsApplicationJson<TResult>(in SimpleHttpContext context, in TResult value)
    {
        context.HttpResponseFeature.StatusCode = 200;
        context.HttpResponseFeature.Headers.ContentType = WellKnownMimes.ApplicationJson;
        return JsonSerializer.SerializeAsync(utf8Json: context.HttpResponseBodyFeature.Stream, value: value, cancellationToken: context.CancellationToken);
    }
}
