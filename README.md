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


TODO:
There is 3 monitors instead of 2.
Behavior:
Cancel all tasks if reconnecting with new connection. (behavior, "throw if lost session" ?)
Reconnect on connection lost.
There is issues on reconnection with the IdStore: After CanceAllAcks, the IdStore does not contain any IDs
Concurrency issues due to disconnect.
Publish may fail if we publish before stopwatch ticks did not increase.
QoS is bugged.
Write test where we send more than 65 packets.

Spec not yet implemented:
MQTT-1.5.3-1 The character data in a UTF-8 encoded string MUST be well-formed UTF-8 as defined by the Unicode specification [Unicode] and restated in RFC 3629 [RFC3629]. In particular this data MUST NOT include encodings of code points between U+D800 and U+DFFF. If a Server or Client receives a Control Packet containing ill-formed UTF-8 it MUST close the Network Connection
