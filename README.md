# Getting started:

TODO, See project 'SimpleClientTest' for now.

## Ack Tasks
You can notice something weird on our API:
A lot of methods return a `ValueTask<Task>`
But why are we doing this 'horrible' thing ?
This is simple:
The first `ValueTask` ensure that the client have stored the packet in it's store.
When this `ValueTask` is completed, the client satisfy the QoS guarantees.
The Task will complete when the Server acknowlodged the message. 
## PipeWriter Readings
PipeWriter are not easy to use, and at the time I'm writing this, the XML Docs are scarce in details.
So I compiled some reading for you.
First thing to read: https://docs.microsoft.com/en-us/dotnet/standard/io/pipelines
When should you flush the pipe ? https://github.com/dotnet/runtime/issues/26747#issuecomment-403892674


## TODO:
MQTTIdStore<T> : IOutgoingPacketStore:
Remove interface, use abstract method instead.

Document and implement a full object lifecycle.

### 1.0.0:
#### Client is usable:
Session related things doesn't work well.
Automated reconnection (option is here but does it work ?).
Split regular/advanced API in the namespaces.

#### Features that we want:
Store.OnPacketSent is not called anymore. (Used to tag dead packet)

#### Resiliency
An exception outside the try/catch in the input loop doesnt kill the client.
Cancel all tasks if reconnecting with new connection. (behavior, "throw if lost session" ?)
Determine when the store must have storing guarenties, and when we don't care that it stored right now the data.
    Synch => we don't care that the data is stored right now.
    Async => must be stored when async end.
Currently this is not checked: MQTT-1.5.3-1 The character data in a UTF-8 encoded string MUST be well-formed UTF-8 as defined by the Unicode specification [Unicode] and restated in RFC 3629 [RFC3629]. In particular this data MUST NOT include encodings of code points between U+D800 and U+DFFF. If a Server or Client receives a Control Packet containing ill-formed UTF-8 it MUST close the Network Connection


#### Tests
Write test where we send more than IdStore(startCount) packets.

#### Cleaning
Fixes namespaces.
Fixes all accessibility modifier to what it should be.
IInputLogger contain Client Reflex loggers. It cause the Server to have

#### Final pass
Logger methods match their context (after refactoring that may not be the case anymore).

#### Docs
A doc per project.
A good readme.
All public API documented.

### Future:
Useless alloc: instead of allocating Publish object I could just write them directly into a buffer and queue it.
