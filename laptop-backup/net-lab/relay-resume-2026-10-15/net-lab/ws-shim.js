/**
 * ws-shim.js — minimal server-side WebSocket for the lobby-lab (AI-097).
 *
 * The lab's node http server needs to terminate WebSocket upgrades for
 * local relay testing without adding dependencies. This implements the
 * RFC 6455 server side with the standard library only:
 *
 * - `handleUpgrade(req, socket, head)` validates the handshake
 *   (GET + Upgrade: websocket + Sec-WebSocket-Key + version 13) and
 *   writes the 101 response with the SHA-1 accept key.
 * - The returned socket wrapper speaks frames: masked client text frames
 *   are unmasked and reassembled (continuations supported); ping is
 *   answered with pong; close is echoed.
 * - `send(text)` writes a single unmasked server text frame.
 *
 * Local development only. Not a production WebSocket server: no
 * compression, no fragmentation of outbound frames, 8 MB inbound cap.
 */

import { createHash } from "node:crypto";

const WS_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
const MAX_INBOUND_BYTES = 8 * 1024 * 1024;

function acceptKey(key) {
    return createHash("sha1").update(key + WS_GUID).digest("base64");
}

/**
 * Validate the upgrade handshake and take over the socket.
 * Returns a wrapper {onMessage, onClose, send, close, closed} or null
 * when the handshake is invalid (the caller should destroy the socket).
 */
export function handleUpgrade(req, socket, head) {
    const key = req.headers["sec-websocket-key"];
    const upgrade = (req.headers["upgrade"] || "").toLowerCase();
    const version = req.headers["sec-websocket-version"];
    if (req.method !== "GET" || upgrade !== "websocket" || !key || version !== "13") {
        return null;
    }
    const accept = acceptKey(key.trim());
    socket.write(
        "HTTP/1.1 101 Switching Protocols\r\n" +
        "Upgrade: websocket\r\n" +
        "Connection: Upgrade\r\n" +
        `Sec-WebSocket-Accept: ${accept}\r\n` +
        "\r\n",
    );

    const listeners = { message: [], close: [] };
    let buf = Buffer.concat([head]);
    let closed = false;
    let fragOpcode = null;
    let fragParts = [];

    function emitClose() {
        if (closed) return;
        closed = true;
        for (const cb of listeners.close) {
            try { cb(); } catch { /* noop */ }
        }
    }

    function sendFrame(opcode, payload) {
        if (closed) return;
        const len = payload.length;
        let header;
        if (len < 126) {
            header = Buffer.from([0x80 | opcode, len]);
        } else if (len < 65536) {
            header = Buffer.alloc(4);
            header[0] = 0x80 | opcode;
            header[1] = 126;
            header.writeUInt16BE(len, 2);
        } else {
            header = Buffer.alloc(10);
            header[0] = 0x80 | opcode;
            header[1] = 127;
            header.writeBigUInt64BE(BigInt(len), 2);
        }
        try {
            socket.write(Buffer.concat([header, payload]));
        } catch {
            emitClose();
        }
    }

    function dispatchMessage(data) {
        const text = data.toString("utf8");
        for (const cb of listeners.message) {
            try { cb(text); } catch { /* listener errors must not kill the socket */ }
        }
    }

    function onControl(opcode, payload) {
        if (opcode === 0x9) sendFrame(0xa, payload); // ping -> pong
        if (opcode === 0x8) {
            // Echo the close and shut down.
            sendFrame(0x8, payload);
            try { socket.end(); } catch { /* noop */ }
            emitClose();
        }
    }

    function onData(opcode, payload) {
        if (opcode === 0x1 || opcode === 0x2) {
            if (fragOpcode !== null) return; // protocol error; drop
            fragOpcode = opcode;
            fragParts = [payload];
        } else if (opcode === 0x0) {
            if (fragOpcode === null) return; // stray continuation; drop
            fragParts.push(payload);
        } else {
            return;
        }
        // FIN is checked by the caller before invoking onData.
        const message = Buffer.concat(fragParts);
        fragOpcode = null;
        fragParts = [];
        dispatchMessage(message);
    }

    function parse() {
        while (buf.length >= 2) {
            const b0 = buf[0];
            const b1 = buf[1];
            const fin = (b0 & 0x80) !== 0;
            const opcode = b0 & 0x0f;
            const masked = (b1 & 0x80) !== 0;
            let len = b1 & 0x7f;
            let off = 2;
            if (len === 126) {
                if (buf.length < 4) return;
                len = buf.readUInt16BE(2);
                off = 4;
            } else if (len === 127) {
                if (buf.length < 10) return;
                const big = buf.readBigUInt64BE(2);
                if (big > BigInt(MAX_INBOUND_BYTES)) {
                    try { socket.destroy(); } catch { /* noop */ }
                    emitClose();
                    return;
                }
                len = Number(big);
                off = 10;
            }
            const maskOff = off;
            off += masked ? 4 : 0;
            if (buf.length < off + len) return; // wait for more data
            // Server side MUST receive masked frames from clients.
            if (!masked) {
                try { socket.destroy(); } catch { /* noop */ }
                emitClose();
                return;
            }
            const mask = buf.subarray(maskOff, maskOff + 4);
            const payload = Buffer.alloc(len);
            for (let i = 0; i < len; i++) payload[i] = buf[off + i] ^ mask[i % 4];
            buf = buf.subarray(off + len);

            if (opcode >= 0x8) {
                if (!fin) { try { socket.destroy(); } catch { /* noop */ } emitClose(); return; }
                onControl(opcode, payload);
            } else if (opcode === 0x0 || opcode === 0x1 || opcode === 0x2) {
                if (fin) onData(opcode, payload);
                else {
                    // Start/continue a fragmented message.
                    if (opcode === 0x1 || opcode === 0x2) {
                        if (fragOpcode !== null) return;
                        fragOpcode = opcode;
                        fragParts = [payload];
                    } else if (fragOpcode !== null) {
                        fragParts.push(payload);
                    }
                }
            } else {
                try { socket.destroy(); } catch { /* noop */ }
                emitClose();
                return;
            }
        }
    }

    socket.on("data", (chunk) => {
        if (closed) return;
        buf = Buffer.concat([buf, chunk]);
        if (buf.length > MAX_INBOUND_BYTES) {
            try { socket.destroy(); } catch { /* noop */ }
            emitClose();
            return;
        }
        parse();
    });
    // Half-close from the client: end our side so the socket fully closes
    // (otherwise an http server.close() waits on it forever).
    socket.on("end", () => {
        try { socket.end(); } catch { /* noop */ }
    });
    socket.on("close", emitClose);
    socket.on("error", emitClose);

    // The handshake data may already be buffered in `head`.
    if (buf.length > 0) parse();

    return {
        get closed() { return closed; },
        onMessage(cb) { listeners.message.push(cb); },
        onClose(cb) { listeners.close.push(cb); },
        send(text) {
            sendFrame(0x1, Buffer.from(String(text), "utf8"));
        },
        close() {
            if (!closed) {
                sendFrame(0x8, Buffer.alloc(0));
                try { socket.end(); } catch { /* noop */ }
                emitClose();
            }
        },
    };
}
