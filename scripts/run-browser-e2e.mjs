import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const run = (command, args, options = {}) => {
  const result = spawnSync(command, args, {
    cwd: options.cwd ?? root,
    env: process.env,
    encoding: "utf8",
    stdio: options.capture ? "pipe" : "inherit",
  });
  if (result.error) throw result.error;
  if (result.status !== 0) {
    if (options.capture) {
      process.stderr.write(result.stdout ?? "");
      process.stderr.write(result.stderr ?? "");
    }
    throw new Error(`${command} ${args.join(" ")} exited with ${result.status}`);
  }
  return result.stdout ?? "";
};

const requireStoppedStack = () => {
  // The E2E runner tears down the entire shared Compose project. Checking only
  // for the backend misses a running repo editor, database, Azurite, agent, or tools-profile container.
  const activeServices = run("docker", [
    "compose", "--profile", "*", "ps", "--status", "running", "--services",
  ], { capture: true }).trim();
  if (!activeServices) return;

  throw new Error(
    `Browser E2E requires exclusive ownership of the Planarian Compose stack. ` +
    `Already running: ${activeServices.split("\n").join(", ")}. ` +
    `Stop only services you own before running E2E.`
  );
};

requireStoppedStack();

let ownsStack = false;
try {
  process.env.REACT_APP_API_ORIGIN_MAPPINGS =
    '{"planarian.local":"https://planarian.local:8443"}';
  run("npm", ["run", "build"], { cwd: path.join(root, "Planarian.Web") });
  process.env.PLANARIAN_FRONTEND_PREBUILT = "1";

  // The production build can take long enough for another local workflow to
  // start a stack after the initial guard. Re-check immediately before the
  // destructive disposable-data reset rather than relying on stale status.
  requireStoppedStack();
  // From this point forward the runner owns any infrastructure it creates,
  // including a partially completed reset/startup that subsequently fails.
  ownsStack = true;
  run("./dev", ["reset", "e2e"]);
  run("./dev", ["up", "e2e"]);

  const playwrightArgs = ["playwright", "test", ...process.argv.slice(2)];
  run("npx", playwrightArgs, { cwd: path.join(root, "Planarian.Web") });
} finally {
  if (ownsStack) {
    run("./dev", ["down"]);
  }
}
