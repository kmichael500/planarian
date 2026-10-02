import { cp, mkdir, rm } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const webRoot = path.join(repositoryRoot, "Planarian.Web");
const nodeModules = path.join(webRoot, "node_modules");
const outputRoot = path.join(webRoot, "public", "pdfjs");

const assets = [
  ["pdfjs-dist/cmaps", "cmaps"],
  ["pdfjs-dist/iccs", "iccs"],
  ["pdfjs-dist/standard_fonts", "standard_fonts"],
  ["pdfjs-dist/wasm", "wasm"],
  ["pdfjs-dist/build/pdf.sandbox.mjs", "pdf.sandbox.mjs"],
  ["pdfjs-viewer-element/dist", "viewer"],
];

async function main() {
  await rm(outputRoot, { recursive: true, force: true });
  await mkdir(outputRoot, { recursive: true });

  for (const [source, destination] of assets) {
    await cp(path.join(nodeModules, source), path.join(outputRoot, destination), {
      recursive: true,
    });
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
});
