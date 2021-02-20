# Getting started:

TODO.
See project 'SimpleClientTest' for now.

## Ack Tasks
You can notice something weird on our API:
A lot of methods return a ValueTask<Task>
But why are we doing this 'horrible' thing ?
This is simple:
The first ValueTask ensure that the client have stored the packet in it's store.
When this ValueTask is completed, the client guarantee that it will retry to send the packet.

## PipeWriter Readings
PipeWriter are not easy to use, and at the time I'm writing this, the XML Docs are scarce in details.
So I compiled some reading for you.
First thing to read: https://docs.microsoft.com/en-us/dotnet/standard/io/pipelines
When should you flush the pipe ? https://github.com/dotnet/runtime/issues/26747#issuecomment-403892674


TODO:
MqttIdStore: not thread safe currently, it should be.
Store.OnPacketSent is not called anymore.
an exception outside the try/catch in the input loop doesnt kill the client.

Cancel all tasks if reconnecting with new connection. (behavior, "throw if lost session" ?)
Reconnect on connection lost.
There is issues on reconnection with the IdStore: After CanceAllAcks, the IdStore does not contain any IDs
Concurrency issues due to disconnect.
Write test where we send more than IdStore(startCount) packets.
Determine when the store must have storing guarenties, and when we don't care that it stored right now the data.
    Synchron => we don't care that the data is stored right now.
    Async => must be stored when async end.


Backpressure logic:
 Block write, or grow buffer, or both ?
 It mean I will need to rewrite a part in the store.
 Currently packet Id are assigned before entering in the store.
 We can also not do it and says it's not our job.

Useless alloc: instead of allocating Publish object I can just write them directly into the store.


Spec not yet implemented:
MQTT-1.5.3-1 The character data in a UTF-8 encoded string MUST be well-formed UTF-8 as defined by the Unicode specification [Unicode] and restated in RFC 3629 [RFC3629]. In particular this data MUST NOT include encodings of code points between U+D800 and U+DFFF. If a Server or Client receives a Control Packet containing ill-formed UTF-8 it MUST close the Network Connection

Things to do for the 1.0.0:
Fixes namespaces.
Fixes all accessibility modifier to what it should be.
