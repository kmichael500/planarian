import assert from "node:assert/strict";
import test from "node:test";
import {
  buildCsharp,
  buildTypescript,
  routeParameterNames,
  validateManifest,
} from "./generate-client-routes.mjs";

const manifest = (routes) => ({ routes });

test("infers multiple full-segment path parameters in order", () => {
  assert.deepEqual(
    routeParameterNames("/projects/:projectId/trips/:tripId"),
    ["projectId", "tripId"]
  );
  assert.deepEqual(routeParameterNames("/literal/prefix-:notAParameter"), []);
});

test("rejects malformed or ambiguous route contracts", () => {
  assert.throws(() => validateManifest(null), /routes object/);
  assert.throws(
    () => validateManifest(manifest({ bad: { path: "relative" } })),
    /absolute client path/
  );
  assert.throws(
    () => validateManifest(manifest({ bad: { path: "/x", queryParameters: "code" } })),
    /must be an array/
  );
  assert.throws(
    () => validateManifest(manifest({ "bad-name": { path: "/x" } })),
    /generated-code-safe identifier/
  );
  assert.throws(
    () => validateManifest(manifest({ bad: { path: "/x/:id", queryParameters: ["id"] } })),
    /must be unique within the route/
  );
  assert.throws(
    () => validateManifest(manifest({ one: { path: "/x/:id" }, two: { path: "/x/:name" } })),
    /indistinguishable/
  );
  assert.throws(
    () => validateManifest(manifest({ one: { path: "/Users/:id" }, two: { path: "/users/:name" } })),
    /indistinguishable/
  );
  assert.throws(
    () => validateManifest(manifest({ reserved: { path: "/x/:class" } })),
    /parameter name class is reserved/
  );
  assert.throws(
    () => validateManifest(manifest({ foo: { path: "/foo" }, Foo: { path: "/bar" } })),
    /generated C# route class collides/
  );
  assert.throws(
    () => validateManifest(manifest({ path: { path: "/otherwise-valid" } })),
    /conflicts with generated route members/
  );
  assert.throws(
    () => validateManifest(manifest({ clientRoutes: { path: "/otherwise-valid" } })),
    /conflicts with generated route members/
  );
});

test("rejects unsupported or non-canonical path patterns explicitly", () => {
  assert.throws(
    () => validateManifest(manifest({ optional: { path: "/:lang?/items" } })),
    /optional segments are not supported yet/
  );
  assert.throws(
    () => validateManifest(manifest({ splat: { path: "/files/*" } })),
    /splat routes are not supported/
  );
  assert.throws(
    () => validateManifest(manifest({ partial: { path: "/items/prefix-:id" } })),
    /full path segment/
  );
  assert.throws(
    () => validateManifest(manifest({ trailing: { path: "/items/" } })),
    /trailing slashes are not allowed/
  );
});

test("folds the first query parameter into a static route literal", () => {
  const input = manifest({
    confirm: {
      path: "/confirm-email",
      queryParameters: ["code", "redirect"],
    },
  });

  const typescript = buildTypescript(input);
  assert.match(
    typescript,
    /return "\/confirm-email\?code=" \+ encodeURIComponent\(code\) \+ "&redirect=" \+ encodeURIComponent\(redirect\);/
  );
  assert.doesNotMatch(typescript, /"\/confirm-email" \+ "\?code="/);

  const csharp = buildCsharp(input);
  assert.match(
    csharp,
    /=> "\/confirm-email\?code=" \+ Uri\.EscapeDataString\(code\) \+ "&redirect=" \+ Uri\.EscapeDataString\(redirect\);/
  );
});

test("generates builders for multiple path and query parameters", () => {
  const input = manifest({
    sample: {
      path: "/projects/:projectId/trips/:tripId",
      queryParameters: ["view", "filter"],
    },
  });

  const typescript = buildTypescript(input);
  assert.match(typescript, /get\(projectId: string, tripId: string, view: string, filter: string\)/);
  assert.match(typescript, /encodeURIComponent\(projectId\)/);
  assert.match(typescript, /encodeURIComponent\(tripId\)/);
  assert.match(typescript, /\?view=/);
  assert.match(typescript, /&filter=/);

  const csharp = buildCsharp(input);
  assert.match(csharp, /Get\(string projectId, string tripId, string view, string filter\)/);
  assert.match(csharp, /Uri\.EscapeDataString\(projectId\)/);
  assert.match(csharp, /Uri\.EscapeDataString\(tripId\)/);
  assert.doesNotMatch(csharp, /public const string Prefix/);
});

test("emits a static C# prefix only for a single terminal path parameter", () => {
  const csharp = buildCsharp(manifest({ detail: { path: "/items/:itemId" } }));
  assert.match(csharp, /public const string Prefix = "\/items\/";/);
});
