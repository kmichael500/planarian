import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtemp, rm, writeFile, chmod } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

test("browser E2E refuses an active Compose service even when the backend is absent (including a tools-profile service)", async () => {
  const temp = await mkdtemp(path.join(os.tmpdir(), "planarian-browser-runner-"));
  try {
    const docker = path.join(temp, "docker");
    await writeFile(docker, '#!/bin/sh\nprintf "tooling\\npostgres\\n"\n');
    await chmod(docker, 0o755);
    const result = spawnSync(process.execPath, ["scripts/run-browser-e2e.mjs", "--list"], {
      cwd: root,
      env: { ...process.env, PATH: `${temp}${path.delimiter}${process.env.PATH ?? ""}` },
      encoding: "utf8",
    });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /exclusive ownership/);
    assert.match(result.stderr, /tooling, postgres/);
    assert.doesNotMatch(result.stdout, /Building production frontend|Resetting all local data/);
  } finally {
    await rm(temp, { recursive: true, force: true });
  }
});
