/**
 * TRACER WebGL transport: drives jszmq sockets from Unity.
 *
 * WebGL builds have no System.Net.Sockets and no usable threading, so the
 * sockets live in JavaScript. Received messages are queued here and drained by
 * C# from the main thread, which keeps the whole transport single threaded and
 * frame synchronous.
 *
 * Requires the vendored jszmq bundle to have been loaded before Unity boots;
 * see Assets/WebGLTemplates. It exposes globalThis.jszmq and globalThis.Buffer.
 */

mergeInto(LibraryManager.library, {

  $TZ: {
    sockets: {},   // handle -> { socket, type, queue, awaitingReply, outgoing }
    nextHandle: 1,

    get: function (handle) {
      return TZ.sockets[handle];
    },
  },

  // ----------------------------------------------------------------- create

  //! type: 0 Subscriber, 1 Publisher, 2 Request, 3 Response
  TracerZmq_Create__deps: ["$TZ"],
  TracerZmq_Create: function (type) {
    if (typeof jszmq === "undefined") {
      console.error("[TracerZmq] jszmq is not loaded. The WebGL template must " +
                    "include the vendored jszmq bundle before the Unity loader.");
      return 0;
    }

    var socket;
    switch (type) {
      case 0: socket = new jszmq.Sub(); break;
      case 1: socket = new jszmq.Pub(); break;
      case 2: socket = new jszmq.Req(); break;
      default:
        // Response sockets must bind, which a browser cannot do.
        console.error("[TracerZmq] socket type " + type + " is not available in a browser");
        return 0;
    }

    var handle = TZ.nextHandle++;
    var entry = {
      socket: socket,
      type: type,
      queue: [],           // array of arrays of Uint8Array (one entry per message)
      awaitingReply: false,
      outgoing: [],        // multipart frames being assembled
    };
    TZ.sockets[handle] = entry;

    socket.on("message", function () {
      var frames = [];
      for (var i = 0; i < arguments.length; i++)
        frames.push(new Uint8Array(arguments[i]));
      entry.queue.push(frames);
      // A reply landed, so the Request socket may send again.
      entry.awaitingReply = false;
    });

    return handle;
  },

  // -------------------------------------------------------------- endpoints

  TracerZmq_Connect__deps: ["$TZ"],
  TracerZmq_Connect: function (handle, addressPtr) {
    var entry = TZ.get(handle);
    if (!entry) return 0;
    try {
      entry.socket.connect(UTF8ToString(addressPtr));
      return 1;
    } catch (e) {
      console.error("[TracerZmq] connect failed: " + e);
      return 0;
    }
  },

  TracerZmq_Subscribe__deps: ["$TZ"],
  TracerZmq_Subscribe: function (handle) {
    var entry = TZ.get(handle);
    if (!entry) return 0;
    try {
      entry.socket.subscribe("");   // TRACER uses no topic frames
      return 1;
    } catch (e) {
      console.error("[TracerZmq] subscribe failed: " + e);
      return 0;
    }
  },

  // ------------------------------------------------------------------ send

  //! Request sockets are strict lockstep: no send while a reply is outstanding.
  TracerZmq_HasOut__deps: ["$TZ"],
  TracerZmq_HasOut: function (handle) {
    var entry = TZ.get(handle);
    if (!entry) return 0;
    if (entry.type === 2 && entry.awaitingReply) return 0;
    return 1;
  },

  TracerZmq_AddFrame__deps: ["$TZ"],
  TracerZmq_AddFrame: function (handle, dataPtr, length) {
    var entry = TZ.get(handle);
    if (!entry) return 0;
    // slice, not subarray: the Unity heap may be reused before send() reads it.
    entry.outgoing.push(HEAPU8.slice(dataPtr, dataPtr + length));
    return 1;
  },

  //! Send every frame added since the last flush as one multipart message.
  TracerZmq_Flush__deps: ["$TZ"],
  TracerZmq_Flush: function (handle) {
    var entry = TZ.get(handle);
    if (!entry) return 0;
    if (entry.outgoing.length === 0) return 0;

    var frames = entry.outgoing;
    entry.outgoing = [];

    try {
      var buffers = frames.map(function (f) { return Buffer.from(f); });
      entry.socket.send(buffers);
      if (entry.type === 2) entry.awaitingReply = true;
      return 1;
    } catch (e) {
      console.error("[TracerZmq] send failed: " + e);
      return 0;
    }
  },

  // --------------------------------------------------------------- receive

  //! Number of frames in the oldest queued message, or -1 when nothing is queued.
  TracerZmq_PeekFrameCount__deps: ["$TZ"],
  TracerZmq_PeekFrameCount: function (handle) {
    var entry = TZ.get(handle);
    if (!entry || entry.queue.length === 0) return -1;
    return entry.queue[0].length;
  },

  TracerZmq_PeekFrameSize__deps: ["$TZ"],
  TracerZmq_PeekFrameSize: function (handle, index) {
    var entry = TZ.get(handle);
    if (!entry || entry.queue.length === 0) return -1;
    var message = entry.queue[0];
    if (index < 0 || index >= message.length) return -1;
    return message[index].length;
  },

  //! Copy one frame of the oldest message into a managed buffer.
  TracerZmq_CopyFrame__deps: ["$TZ"],
  TracerZmq_CopyFrame: function (handle, index, bufferPtr, maxLength) {
    var entry = TZ.get(handle);
    if (!entry || entry.queue.length === 0) return -1;
    var message = entry.queue[0];
    if (index < 0 || index >= message.length) return -1;

    var frame = message[index];
    if (frame.length > maxLength) return -1;

    HEAPU8.set(frame, bufferPtr);
    return frame.length;
  },

  //! Discard the oldest message once every frame has been copied out.
  TracerZmq_PopMessage__deps: ["$TZ"],
  TracerZmq_PopMessage: function (handle) {
    var entry = TZ.get(handle);
    if (!entry || entry.queue.length === 0) return 0;
    entry.queue.shift();
    return 1;
  },

  // ----------------------------------------------------------------- close

  TracerZmq_Close__deps: ["$TZ"],
  TracerZmq_Close: function (handle) {
    var entry = TZ.get(handle);
    if (!entry) return;
    try {
      entry.socket.close();
    } catch (e) {
      // already gone
    }
    delete TZ.sockets[handle];
  },
});
