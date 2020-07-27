# Multithreading issues:
This mqtt client should have few multithreading issues: Emissions are unparallelised through a channel, reception is non concurrent.
The biggest source of issue, unknowns, and most dangerous thing I known in this client is the IdStore `TaskCompletionSource`s.

# About reusability:
One of the first thought about this, is the fact that the IncomingMessageHandler and OutgoingMessageHandlers looks generic enough so it can be reused for other projects.
While writing documentation, I noticed that the Reflex have protocol specifity of MQTT, here it's signature:
```csharp
Reflex( IMqttLogger m, IncomingMessageHandler sender, byte header, int packetSize, PipeReader reader );
```
We can see 2 things: the `byte header` and `int packetSize`, both of these fields exist due to mqtt.
If we want to make it reusable for another protocol, a lot of the code would be gone, only the loop, and the lifecycle, and a few local variable would survive.
And it would be great, it would separate the lifecycle (loop/stop) and the parsing of the header.

It took me really a long time to understand this, and why I felt this codebase was better than other mqtt libraries:
The best codebase don't have a perfect API, better performance, or achitecture, but is the most maintenable.
The existing code is useless if you can't change it due to your unit test(Xamarin.MQTT).
