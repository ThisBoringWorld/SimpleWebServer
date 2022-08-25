using System.Collections.Immutable;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.SimpleServer;

internal class SimpleHttpApplication : IHttpApplication<IFeatureCollection>
{
    private readonly ImmutableDictionary<string, ImmutableDictionary<string, SimpleRequestProcessDelegate>> _requestMethodMap;

    public SimpleHttpApplication(ImmutableDictionary<string, ImmutableDictionary<string, SimpleRequestProcessDelegate>> requestMethodMap)
    {
        _requestMethodMap = requestMethodMap ?? throw new ArgumentNullException(nameof(requestMethodMap));
    }

    public IFeatureCollection CreateContext(IFeatureCollection contextFeatures) => contextFeatures;

    public void DisposeContext(IFeatureCollection contextFeatures, Exception? exception)
    { }

    public Task ProcessRequestAsync(IFeatureCollection contextFeatures)
    {
        if (contextFeatures.Get<IHttpResponseFeature>() is not IHttpResponseFeature httpResponseFeature
            || contextFeatures.Get<IHttpResponseBodyFeature>() is not IHttpResponseBodyFeature httpResponseBodyFeature
            || contextFeatures.Get<IHttpRequestLifetimeFeature>() is not IHttpRequestLifetimeFeature httpRequestLifetimeFeature)
        {
            return Task.CompletedTask;
        }

        if (contextFeatures.Get<IHttpRequestFeature>() is IHttpRequestFeature httpRequestFeature
            && _requestMethodMap.TryGetValue(httpRequestFeature.Method, out var pathMap)
            && pathMap.TryGetValue(httpRequestFeature.Path, out var requestProcessDelegate))
        {
            return requestProcessDelegate(new(contextFeatures, httpRequestFeature, httpResponseFeature, httpResponseBodyFeature, httpRequestLifetimeFeature.RequestAborted));
        }

        httpResponseFeature.StatusCode = 404;

        return Task.CompletedTask;
    }
}
