import { createReadStream } from "node:fs";
import { access, readFile, stat } from "node:fs/promises";
import { createServer } from "node:http";
import path from "node:path";

const root = path.resolve(process.argv[2] ?? "build");
const port = Number(process.env.PORT ?? 3000);
const config = JSON.parse(await readFile(path.join(root, "staticwebapp.config.json"), "utf8"));
const globalHeaders = config.globalHeaders ?? {};

const mimeTypes = new Map([
  [".css", "text/css; charset=utf-8"],
  [".gif", "image/gif"],
  [".html", "text/html; charset=utf-8"],
  [".ico", "image/x-icon"],
  [".jpeg", "image/jpeg"],
  [".jpg", "image/jpeg"],
  [".js", "text/javascript; charset=utf-8"],
  [".json", "application/json; charset=utf-8"],
  [".map", "application/json; charset=utf-8"],
  [".mjs", "text/javascript; charset=utf-8"],
  [".png", "image/png"],
  [".svg", "image/svg+xml"],
  [".txt", "text/plain; charset=utf-8"],
  [".wasm", "application/wasm"],
  [".woff", "font/woff"],
  [".woff2", "font/woff2"],
]);

const resolveFile = async (requestPath) => {
  const decoded = decodeURIComponent(requestPath.split("?")[0]);
  const normalized = path.normalize(decoded).replace(/^([/\\])+/, "");
  const candidate = path.resolve(root, normalized || "index.html");
  if (!candidate.startsWith(root + path.sep) && candidate !== root) return null;

  try {
    const info = await stat(candidate);
    if (info.isFile()) return candidate;
  } catch {
    // SPA fallback below.
  }

  if (!path.extname(normalized)) return path.join(root, "index.html");
  return null;
};

const server = createServer(async (request, response) => {
  try {
    const file = await resolveFile(request.url ?? "/");
    if (!file) {
      response.writeHead(404, globalHeaders);
      response.end("Not found");
      return;
    }

    await access(file);
    const info = await stat(file);
    const headers = {
      ...globalHeaders,
      "Content-Type": mimeTypes.get(path.extname(file).toLowerCase()) ?? "application/octet-stream",
      "Content-Length": String(info.size),
    };

    response.writeHead(200, headers);
    if (request.method === "HEAD") {
      response.end();
      return;
    }
    createReadStream(file).pipe(response);
  } catch (error) {
    response.writeHead(500, { "Content-Type": "text/plain; charset=utf-8" });
    response.end(error instanceof Error ? error.message : "Static server error");
  }
});

server.listen(port, "0.0.0.0", () => {
  console.log(`Serving production frontend from ${root} on port ${port}`);
});
