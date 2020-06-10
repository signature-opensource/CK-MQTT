These are notes written the 09/06/2020, progress on understanding the codebase has been made since.

** Aedes codebase
Aedes is not the Hadès, as i tought, but it's mosquito, and describe well how I feel about this codebase.
Variable naming does not follow the specs naming, making it harder to read
"qos-packet" => PacketIdentifier
Use JS, not TS.
There is a type definition file. It contain a lot of "any".
When type are defined, it still use fields not defined in the interface.
I think there is other good idea in this codebase, prominently displayed as "middleware" but i can't wrap my head about what is their use.
I think I may have lost now 30 minutes because someone tought "hey this object does not care about a client, does not need a client, but my callback need my client so i will put it here..."
Still cant wrap my head around what does the mqtt emitter.


Did I mentionned they don't use semicolon at all ?
Complex flow are badly intendated/spaced

This codebase contain half of what I hate about node stuff.
*** architecture :
incoming data is poured into a mqtt-packet parser.
the parser add the incoming data in a list, and when it get enough data, raise an event with the deserialized packet.
MQTTnet behave more or less the same on this side.
The rest of the protocol management is made in a method handle, a big switch-case on the packet name. (And no, not the packet ID but a full string).

** Data persistence:
The API is nice, it encapsulate more or less every "usage" of the retained message.
Source: https://www.npmjs.com/package/aedes-persistence
The only downside is this interface encapsulate too much things. But maybe making an interface for each subject would be over engineer.
usual "wtf MQTTnet": 
    - https://github.com/chkr1011/MQTTnet/blob/master/Source/MQTTnet/Server/IMqttServerStorage.cs
    - https://github.com/chkr1011/MQTTnet/blob/master/Source/MQTTnet/Server/IMqttRetainedMessagesManager.cs

** Parsing/Serialising:
They use https://github.com/mqttjs/mqtt-packet.
The packet payload use Node's "Buffer", so it's a simple byte array.
They serialize the packets using objects representing them.
They deserialize the packets using the same objects.
Nothing to see here, what we want to do is far better.
Because we don't delegate parsing/serialising, we should be faster, better handle DOS attacks, etc.
Instead of allocating an object, to act upon, we can simply read the value in a reflex, and execute the needed action, without allocating.



** "Middleware Plugins":
*** persistence
Why is it called middleware ?
The persistence is not a middleware, it's simply an implementation of an imaginary interface.
It's also not plugins, it's simply different implementations that you can use for the persistence.
*** mqemitter
mqemitter does not depend on Aedes, but Aedes depends on it... What ?


** Overflow Strategy:
https://github.com/chkr1011/MQTTnet/blob/master/Source/MQTTnet/Server/MqttPendingMessagesOverflowStrategy.cs
not really used :/ (GitHub return only copy of the implementation, google lead only to website exploiting SEO).

** Tools:
Aedes used https://github.com/krylovsk/mqtt-benchmark to compare with mosca, their old implementation.

** wtf MQTTnet ???
What is this for ?
    - https://github.com/chkr1011/MQTTnet/blob/master/Source/MQTTnet/Server/IMqttServerStoppedHandler.cs
    - https://github.com/chkr1011/MQTTnet/blob/master/Source/MQTTnet/Server/IMqttServerStartedHandler.cs
So the payload is a byte array ?
    - https://github.com/chkr1011/MQTTnet/blob/c22b6033e3eb9195bea9fbd8088a9e551cb75ad6/Source/MQTTnet/MqttApplicationMessage.cs#L11