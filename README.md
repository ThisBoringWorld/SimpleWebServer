# SimpleWebServer

基于 `KestrelServer` 实现的一个具有基本功能的玩具Server（由于没有很多功能，所以简单场景下更快。。。）

```C#
using var server = new SimpleWebServer();

server.MapGet("/api/value", _ => new { Hello = "World" });
server.MapGet("/api/values", _ => new [] { "value1", "value2" });

await server.StartAsync(IPEndPoint.Parse("0.0.0.0:5000"));

Console.WriteLine("按回车退出");
Console.ReadLine();

await server.StopAsync();
```