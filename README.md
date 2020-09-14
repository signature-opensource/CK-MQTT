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
Monitor on IInputLogger.
Behavior:
Cancel all tasks if reconnecting with new connection. (behavior, "throw if lost session" ?)
Reconnect on connection lost.
