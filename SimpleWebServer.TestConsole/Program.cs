using System.Net;

using Microsoft.AspNetCore.SimpleServer;

using var server = new SimpleWebServer();

server.MapGet("/api/value", _ => new { Hello = "World" });
server.MapGet("/api/values", _ => new [] { "value1", "value2" });

await server.StartAsync(IPEndPoint.Parse("0.0.0.0:5000"));

Console.WriteLine("按回车退出");
Console.ReadLine();

await server.StopAsync();
