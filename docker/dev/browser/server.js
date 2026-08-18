const fs = require('fs');
const http = require('http');
const path = require('path');
const { spawn } = require('child_process');
const { chromium } = require('@playwright/test');
const { WebSocketServer, WebSocket } = require('ws');
const { startCdpProxy } = require('./cdp-proxy');

const viewerPort = Number(process.env.VIEWER_PORT || 6080);
const cdpPort = Number(process.env.CDP_PORT || 9222);
const cdpInternalPort = Number(process.env.CDP_INTERNAL_PORT || 9223);
const planarianUrl = process.env.PLANARIAN_URL || 'https://planarian.local/login';
const viewer = fs.readFileSync(path.join(__dirname, 'viewer.html'));
const clients = new Set();

let chrome;
let browser;
let cdpProxy;
let activePage;
let activeSession;
let lastFrame;
let shuttingDown = false;
let exitingForFailure = false;

function failRuntime(message) {
  if (shuttingDown || exitingForFailure) return;
  exitingForFailure = true;
  console.error(message);
  process.exit(1);
}

function broadcast(message) {
  for (const ws of clients) {
    if (ws.readyState === WebSocket.OPEN && ws.bufferedAmount < 2_000_000) {
      ws.send(message);
    }
  }
}
async function attachScreencast(page) {
  if (!page || page === activePage) return;

  if (activeSession) {
    try { await activeSession.send('Page.stopScreencast'); } catch {}
    try { await activeSession.detach(); } catch {}
  }

  activePage = page;
  activeSession = await page.context().newCDPSession(page);
  activeSession.on('Page.screencastFrame', async frame => {
    try { await activeSession.send('Page.screencastFrameAck', { sessionId: frame.sessionId }); } catch {}
    let title = 'Planarian';
    try { title = await page.title(); } catch {}

    lastFrame = JSON.stringify({
      type: 'frame',
      data: `data:image/jpeg;base64,${frame.data}`,
      url: page.url(),
      title
    });
    broadcast(lastFrame);
  });

  await activeSession.send('Page.startScreencast', {
    format: 'jpeg', quality: 65, maxWidth: 1440, maxHeight: 900, everyNthFrame: 2
  });
}
async function waitForCdp() {
  const endpoint = `http://127.0.0.1:${cdpInternalPort}/json/version`;
  for (let attempt = 0; attempt < 100; attempt += 1) {
    try {
      const response = await fetch(endpoint);
      if (response.ok) return;
    } catch {}
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw new Error(`Chromium CDP endpoint did not become ready on port ${cdpPort}.`);
}

async function navigateWhenReady(page) {
  for (let attempt = 0; attempt < 60; attempt += 1) {
    try {
      await page.goto(planarianUrl, { waitUntil: 'domcontentloaded', timeout: 5000 });
      return;
    } catch {
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
  }
  console.warn(`Planarian was not reachable at ${planarianUrl}; browser remains available.`);
}

async function startBrowser() {
  const profileDir = '/tmp/planarian-browser-profile';
  fs.rmSync(profileDir, { recursive: true, force: true });

  chrome = spawn(chromium.executablePath(), [
    '--headless=new',
    '--no-sandbox',
    '--disable-dev-shm-usage',
    '--ignore-certificate-errors',
    `--remote-debugging-port=${cdpInternalPort}`,
    '--remote-debugging-address=127.0.0.1',
    '--remote-allow-origins=*',
    '--user-data-dir=/tmp/planarian-browser-profile',
    '--window-size=1440,900',
    '--no-first-run',
    '--no-default-browser-check',
    'about:blank'
  ], { stdio: ['ignore', 'inherit', 'inherit'] });

  chrome.on('exit', (code, signal) => {
    failRuntime(`Chromium exited unexpectedly (code=${code ?? 'null'}, signal=${signal ?? 'none'}).`);
  });

  await waitForCdp();
  cdpProxy = startCdpProxy({ listenPort: cdpPort, upstreamPort: cdpInternalPort });
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${cdpInternalPort}`);
  browser.on('disconnected', () => failRuntime('Playwright disconnected from Chromium unexpectedly.'));
  const context = browser.contexts()[0];
  context.on('page', page => attachScreencast(page).catch(console.error));

  let page = context.pages()[0];
  if (!page) page = await context.newPage();
  await page.setViewportSize({ width: 1440, height: 900 });
  await attachScreencast(page);
  void navigateWhenReady(page);

  console.log(`BROWSER_READY cdp=http://0.0.0.0:${cdpPort}`);
}

const server = http.createServer((req, res) => {
  if (req.url === '/' || req.url === '/index.html') {
    res.writeHead(200, { 'content-type': 'text/html; charset=utf-8', 'cache-control': 'no-store' });
    return res.end(viewer);
  }
  if (req.url === '/health') {
    const pageUrl = activePage?.url() || null;
    const connected = !!browser && browser.isConnected();
    const chromiumRunning = !!chrome && chrome.exitCode === null && !chrome.killed;
    const ready = connected && chromiumRunning && !!pageUrl &&
      pageUrl !== 'about:blank' && !pageUrl.startsWith('chrome-error:');
    res.writeHead(ready ? 200 : 503, { 'content-type': 'application/json' });
    return res.end(JSON.stringify({ ok: ready, browser: connected, chromium: chromiumRunning, page: pageUrl }));
  }

  res.writeHead(404);
  return res.end('Not found');
});

const wss = new WebSocketServer({ noServer: true });
wss.on('connection', ws => {
  clients.add(ws);
  if (lastFrame) ws.send(lastFrame);
  ws.on('close', () => clients.delete(ws));
});

server.on('upgrade', (req, socket, head) => {
  if (req.url !== '/ws') return socket.destroy();
  wss.handleUpgrade(req, socket, head, ws => wss.emit('connection', ws, req));
});

async function shutdown() {
  shuttingDown = true;
  try { await activeSession?.send('Page.stopScreencast'); } catch {}
  try { await browser?.close(); } catch {}
  try { cdpProxy?.close(); } catch {}
  try { chrome?.kill('SIGTERM'); } catch {}
  server.close(() => process.exit(0));
}

process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);

startBrowser()
  .then(() => server.listen(viewerPort, '0.0.0.0', () => {
    console.log(`VIEWER_READY http://0.0.0.0:${viewerPort}`);
  }))
  .catch(error => {
    console.error(error);
    process.exit(1);
  });
