const http = require('http');
const net = require('net');

function startCdpProxy({ listenPort, upstreamPort }) {
  const server = http.createServer((req, res) => {
    const upstream = http.request({
      hostname: '127.0.0.1',
      port: upstreamPort,
      path: req.url,
      method: req.method,
      headers: { ...req.headers, host: `127.0.0.1:${upstreamPort}` }
    }, upstreamRes => {
      const chunks = [];
      upstreamRes.on('data', chunk => chunks.push(chunk));
      upstreamRes.on('end', () => {
        let body = Buffer.concat(chunks);
        const contentType = upstreamRes.headers['content-type'] || '';
        if (contentType.includes('application/json')) {
          const publicHost = req.headers.host || `browser:${listenPort}`;
          body = Buffer.from(
            body.toString().replaceAll(
              `ws://127.0.0.1:${upstreamPort}`,
              `ws://${publicHost}`
            )
          );
        }
        const responseHeaders = { ...upstreamRes.headers };
        responseHeaders['content-length'] = String(body.length);
        delete responseHeaders['transfer-encoding'];
        res.writeHead(upstreamRes.statusCode || 502, responseHeaders);
        res.end(body);
      });
    });

    upstream.on('error', error => {
      if (!res.headersSent) res.writeHead(502);
      res.end(error.message);
    });
    req.pipe(upstream);
  });

  server.on('upgrade', (req, socket, head) => {
    const upstream = net.connect(upstreamPort, '127.0.0.1', () => {
      const lines = [`${req.method} ${req.url} HTTP/${req.httpVersion}`];
      for (let i = 0; i < req.rawHeaders.length; i += 2) {
        const name = req.rawHeaders[i];
        const value = name.toLowerCase() === 'host'
          ? `127.0.0.1:${upstreamPort}`
          : req.rawHeaders[i + 1];
        lines.push(`${name}: ${value}`);
      }
      upstream.write(`${lines.join('\r\n')}\r\n\r\n`);
      if (head.length) upstream.write(head);
      socket.pipe(upstream).pipe(socket);
    });
    upstream.on('error', () => socket.destroy());
    socket.on('error', () => upstream.destroy());
  });

  server.listen(listenPort, '0.0.0.0');
  return server;
}

module.exports = { startCdpProxy };
