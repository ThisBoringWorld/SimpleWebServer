using System.Collections.Immutable;
using System.Net;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.SimpleServer;

/// <summary>
/// 已知的MIME
/// </summary>
public static class WellKnownMimes
{
    /// <summary>
    /// application/json
    /// </summary>
    public static readonly StringValues ApplicationJson = new("application/json");
}

public sealed class SimpleWebServer : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private KestrelServer? _kestrelServer;
    private ImmutableDictionary<string, ImmutableDictionary<string, SimpleRequestProcessDelegate>> _requestMethodMap;
    private int _state = ServerState.Init;

    public SimpleWebServer(ILoggerFactory? loggerFactory = null)
    {
        _requestMethodMap = ImmutableDictionary.Create<string, ImmutableDictionary<string, SimpleRequestProcessDelegate>>(StringComparer.Ordinal);
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public SimpleWebServer Map(HttpMethod method, string path, SimpleRequestProcessDelegate requestProcessDelegate)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"“{nameof(path)}”不能为 null 或空白。", nameof(path));
        }

        var localMap = _requestMethodMap;

        if (!localMap.TryGetValue(method.Method, out var localPathMap))
        {
            localPathMap = ImmutableDictionary.Create<string, SimpleRequestProcessDelegate>(StringComparer.OrdinalIgnoreCase);
        }

        localPathMap = localPathMap.SetItem(path, requestProcessDelegate);

        localMap = localMap.SetItem(method.Method, localPathMap);

        _requestMethodMap = localMap;

        return this;
    }

    public Task StartAsync(EndPoint endPoint, CancellationToken cancellationToken = default)
    {
        if (_state != ServerState.Init)
        {
            throw new InvalidOperationException($"Server state error: {_state} .");
        }

        _state = ServerState.Starting;

        var transportOptions = new SocketTransportOptions();
        var transportFactory = new SocketTransportFactory(Options.Create(transportOptions), _loggerFactory);

        var kestrelServerOptions = new KestrelServerOptions()
        {
            ApplicationServices = new LoggerFactoryServiceProvider(_loggerFactory),
            AddServerHeader = false,
        };

        kestrelServerOptions.ConfigureEndpointDefaults(options =>
        {
            options.DisableAltSvcHeader = true;
            options.Protocols = HttpProtocols.Http1;
        });

        kestrelServerOptions.Listen(endPoint);

        var kestrelServer = new KestrelServer(Options.Create(kestrelServerOptions), transportFactory, _loggerFactory);

        _kestrelServer = kestrelServer;

        var httpApplication = new SimpleHttpApplication(_requestMethodMap);

        var startTask = kestrelServer.StartAsync(httpApplication, cancellationToken);

        startTask.ContinueWith(task => _state = ServerState.Started, TaskContinuationOptions.OnlyOnRanToCompletion);
        startTask.ContinueWith(task => _state = ServerState.Stoped, TaskContinuationOptions.OnlyOnFaulted);

        return startTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_state != ServerState.Started
            || _state != ServerState.Starting)
        {
            return Task.CompletedTask;
        }
        if (_kestrelServer is KestrelServer server)
        {
            var stopTask = server.StopAsync(cancellationToken);

            stopTask.ContinueWith(task => _state = ServerState.Stoped);

            return stopTask;
        }
        return Task.CompletedTask;
    }

    #region IDisposable

    ~SimpleWebServer()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_state != ServerState.Disposed)
        {
            if (_kestrelServer is KestrelServer server)
            {
                server.Dispose();
            }
            _state = ServerState.Disposed;
        }
        GC.SuppressFinalize(this);
    }

    #endregion IDisposable

    private class LoggerFactoryServiceProvider : IServiceProvider
    {
        private readonly ILoggerFactory _loggerFactory;

        public LoggerFactoryServiceProvider(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(ILoggerFactory))
            {
                return _loggerFactory;
            }
            throw new InvalidOperationException();
        }
    }

    private class ServerState
    {
        public const int Disposed = 5;
        public const int Init = 0;
        public const int Started = 2;
        public const int Starting = 1;
        public const int Stoped = 4;
        public const int Stoping = 3;
    }
}

/// <summary>
/// Http上下文
/// </summary>
/// <param name="ContextFeatures"></param>
/// <param name="HttpRequestFeature"></param>
/// <param name="HttpResponseFeature"></param>
/// <param name="HttpResponseBodyFeature"></param>
/// <param name="CancellationToken"></param>
public record struct SimpleHttpContext(IFeatureCollection ContextFeatures, IHttpRequestFeature HttpRequestFeature, IHttpResponseFeature HttpResponseFeature, IHttpResponseBodyFeature HttpResponseBodyFeature, CancellationToken CancellationToken)
{
    public void Deconstruct(out IFeatureCollection contextFeatures, out CancellationToken cancellationToken)
    {
        contextFeatures = ContextFeatures;
        cancellationToken = CancellationToken;
    }
}

/// <summary>
/// 请求处理委托
/// </summary>
/// <param name="context"></param>
/// <returns></returns>
public delegate Task SimpleRequestProcessDelegate(SimpleHttpContext context);
