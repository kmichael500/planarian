import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptPath = fileURLToPath(import.meta.url);
const scriptDirectory = path.dirname(scriptPath);
const repositoryRoot = path.resolve(scriptDirectory, "..");
const manifestPath = path.join(repositoryRoot, "Contracts", "client-routes.json");
const typescriptPath = path.join(repositoryRoot, "Planarian.Web/src/Configuration/Routing/ClientRoutes.generated.ts");
const csharpPath = path.join(repositoryRoot, "Planarian/Planarian/Shared/Routing/Generated/ClientRoutes.g.cs");
const identifierPattern = /^[A-Za-z][A-Za-z0-9_]*$/;
const reservedParameterNames = new Set([
  "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
  "class", "const", "continue", "debugger", "decimal", "default", "delegate", "delete", "do",
  "double", "else", "enum", "event", "explicit", "export", "extends", "extern", "false", "finally",
  "fixed", "float", "for", "foreach", "function", "goto", "if", "implements", "implicit", "import",
  "in", "instanceof", "int", "interface", "internal", "is", "let", "lock", "long", "namespace",
  "new", "null", "object", "operator", "out", "override", "package", "params", "private", "protected",
  "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static",
  "string", "struct", "super", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
  "unchecked", "unsafe", "ushort", "using", "var", "virtual", "void", "volatile", "while", "with",
  "yield", "await",
]);
const reservedCsharpRouteClassNames = new Set(["ClientRoutes", "Get", "Path", "Prefix"]);

function isObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function pascalCase(value) {
  return value.charAt(0).toUpperCase() + value.slice(1);
}

export function routeParameterNames(routePath) {
  return routePath
    .split("/")
    .filter((segment) => segment.startsWith(":"))
    .map((segment) => segment.slice(1));
}

function routeShape(routePath) {
  return routePath
    .split("/")
    .map((segment) => (segment.startsWith(":") ? ":" : segment.toLowerCase()))
    .join("/");
}

function validateRoute(name, route) {
  if (!identifierPattern.test(name)) {
    throw new Error(`${name}: route name must be a generated-code-safe identifier`);
  }
  if (!isObject(route)) {
    throw new Error(`${name}: route definition must be an object`);
  }
  if (typeof route.path !== "string" || !route.path.startsWith("/")) {
    throw new Error(`${name}: path must be an absolute client path`);
  }
  if (route.path.includes("#")) {
    throw new Error(`${name}: fragments do not belong in a route path`);
  }
  if (route.path.includes("?")) {
    throw new Error(`${name}: optional segments are not supported yet; query parameters belong in queryParameters`);
  }
  if (route.path.includes("*")) {
    throw new Error(`${name}: splat routes are not supported by the client-route generator yet`);
  }
  if (route.path.length > 1 && route.path.endsWith("/")) {
    throw new Error(`${name}: trailing slashes are not allowed; use the canonical path without one`);
  }

  for (const segment of route.path.split("/")) {
    if (segment.includes(":") && !segment.startsWith(":")) {
      throw new Error(`${name}: dynamic parameters must occupy a full path segment: ${segment}`);
    }
    if (segment.startsWith(":") && !/^:[A-Za-z][A-Za-z0-9_]*$/.test(segment)) {
      throw new Error(`${name}: invalid dynamic path segment ${segment}`);
    }
  }

  const queryParameters = route.queryParameters ?? [];
  if (!Array.isArray(queryParameters)) {
    throw new Error(`${name}: queryParameters must be an array of parameter names`);
  }
  for (const parameter of queryParameters) {
    if (typeof parameter !== "string" || !identifierPattern.test(parameter)) {
      throw new Error(`${name}: invalid query parameter name ${String(parameter)}`);
    }
  }

  const allParameters = [...routeParameterNames(route.path), ...queryParameters];
  if (new Set(allParameters).size !== allParameters.length) {
    throw new Error(`${name}: path and query parameter names must be unique within the route`);
  }
  for (const parameter of allParameters) {
    if (reservedParameterNames.has(parameter)) {
      throw new Error(`${name}: parameter name ${parameter} is reserved in generated TypeScript or C#`);
    }
  }
}

export function validateManifest(manifest) {
  if (!isObject(manifest) || !isObject(manifest.routes)) {
    throw new Error("client-routes.json must contain a routes object");
  }

  const routes = Object.entries(manifest.routes);
  if (routes.length === 0) {
    throw new Error("client-routes.json must contain at least one route");
  }

  const seenPaths = new Map();
  const seenShapes = new Map();
  const seenCsharpClassNames = new Map();
  for (const [name, route] of routes) {
    validateRoute(name, route);

    const existingPathName = seenPaths.get(route.path);
    if (existingPathName) {
      throw new Error(`${name}: path duplicates ${existingPathName}: ${route.path}`);
    }
    seenPaths.set(route.path, name);

    const shape = routeShape(route.path);
    const existingShapeName = seenShapes.get(shape);
    if (existingShapeName) {
      throw new Error(`${name}: route shape is indistinguishable from ${existingShapeName}: ${route.path}`);
    }
    seenShapes.set(shape, name);

    const csharpClassName = pascalCase(name);
    if (reservedCsharpRouteClassNames.has(csharpClassName)) {
      throw new Error(`${name}: generated C# route class name ${csharpClassName} conflicts with generated route members`);
    }
    const existingClassName = seenCsharpClassNames.get(csharpClassName);
    if (existingClassName) {
      throw new Error(`${name}: generated C# route class collides with ${existingClassName}: ${csharpClassName}`);
    }
    seenCsharpClassNames.set(csharpClassName, name);
  }

  return routes;
}

function buildPathExpression(routePath, encoder, staticSuffix = "") {
  const parts = [];
  let cursor = 0;
  for (const match of routePath.matchAll(/\/:([A-Za-z][A-Za-z0-9_]*)(?=\/|$)/g)) {
    if (match.index >= cursor) {
      parts.push(JSON.stringify(routePath.slice(cursor, match.index + 1)));
    }
    parts.push(`${encoder}(${match[1]})`);
    cursor = match.index + match[0].length;
  }
  if (cursor < routePath.length) {
    parts.push(JSON.stringify(routePath.slice(cursor) + staticSuffix));
  } else if (staticSuffix) {
    parts.push(JSON.stringify(staticSuffix));
  }
  return parts.length ? parts.join(" + ") : JSON.stringify(routePath + staticSuffix);
}

function routeParameters(route) {
  return [...routeParameterNames(route.path), ...(route.queryParameters ?? [])];
}

function buildGet(route, language) {
  const encoder = language === "typescript" ? "encodeURIComponent" : "Uri.EscapeDataString";
  const type = language === "typescript" ? ": string" : "string ";
  const parameters = routeParameters(route)
    .map((name) => (language === "typescript" ? `${name}${type}` : `${type}${name}`))
    .join(", ");
  const queryParameters = route.queryParameters ?? [];
  const firstQueryParameter = queryParameters.at(0);
  let expression = buildPathExpression(
    route.path,
    encoder,
    firstQueryParameter ? `?${firstQueryParameter}=` : ""
  );

  queryParameters.forEach((name, index) => {
    if (index > 0) expression += ` + ${JSON.stringify(`&${name}=`)}`;
    expression += ` + ${encoder}(${name})`;
  });

  return language === "typescript"
    ? `get(${parameters}): string { return ${expression}; }`
    : `public static string Get(${parameters}) => ${expression};`;
}

export function buildTypescript(manifest) {
  const routes = validateManifest(manifest);
  const lines = [
    "// <auto-generated />",
    "// Source: Contracts/client-routes.json",
    "// Run: npm run generate:client-routes",
    "",
    "export const ClientRoutes = {",
  ];

  for (const [name, route] of routes) {
    lines.push(`  ${name}: {`);
    lines.push(`    path: ${JSON.stringify(route.path)},`);
    lines.push(`    ${buildGet(route, "typescript")},`);
    lines.push("  },");
  }

  lines.push("} as const;", "");
  return lines.join("\n");
}

export function buildCsharp(manifest) {
  const routes = validateManifest(manifest);
  const lines = [
    "// <auto-generated />",
    "// Source: Contracts/client-routes.json",
    "// Run: npm run generate:client-routes",
    "",
    "namespace Planarian.Shared.Routing;",
    "",
    "public static class ClientRoutes",
    "{",
  ];

  for (const [name, route] of routes) {
    lines.push(`    public static class ${pascalCase(name)}`);
    lines.push("    {");
    lines.push(`        public const string Path = ${JSON.stringify(route.path)};`);

    const pathParameters = routeParameterNames(route.path);
    const finalSegment = route.path.split("/").at(-1);
    if (pathParameters.length === 1 && finalSegment?.startsWith(":")) {
      lines.push(`        public const string Prefix = ${JSON.stringify(route.path.slice(0, -finalSegment.length))};`);
    }

    lines.push(`        ${buildGet(route, "csharp")}`);
    lines.push("    }", "");
  }

  lines.push("}", "");
  return lines.join("\n");
}

function writeOrCheck(filePath, content, checkOnly) {
  if (checkOnly) {
    if (!fs.existsSync(filePath) || fs.readFileSync(filePath, "utf8") !== content) {
      console.error(`Generated client routes are stale: ${path.relative(repositoryRoot, filePath)}`);
      process.exitCode = 1;
    }
    return;
  }

  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, content);
}

function runCli() {
  const checkOnly = process.argv.includes("--check");
  const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  writeOrCheck(typescriptPath, buildTypescript(manifest), checkOnly);
  writeOrCheck(csharpPath, buildCsharp(manifest), checkOnly);

  if (!process.exitCode) {
    console.log(checkOnly ? "Client route projections are current." : "Generated client route projections.");
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === scriptPath) {
  runCli();
}
