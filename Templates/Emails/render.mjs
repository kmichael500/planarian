import assert from "node:assert/strict";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import Handlebars from "handlebars";
import mjml2html from "mjml";

const directory = fileURLToPath(new URL(".", import.meta.url));
const source = readFileSync(join(directory, "GenericEmailTemplate.mjml"), "utf8");
const outputFile = join(directory, "GenericEmailTemplate.html");
const { html: compiledHtml, errors } = await mjml2html(source, { validationLevel: "strict" });
assert.equal(errors.length, 0, "MJML must be valid");
const html = compiledHtml.replace(/[ \t]+(?=\r?\n)/g, "").trimEnd() + "\n";

const action = process.argv[2] ?? "check";
if (action === "build") {
  writeFileSync(outputFile, html);
  console.log("Compiled GenericEmailTemplate.html");
} else if (action === "check" || action === "preview") {
  assert.equal(readFileSync(outputFile, "utf8"), html,
    "Compiled HTML is stale. Run npm run build.");
} else {
  throw new Error("Expected build, check, or preview");
}

if (action !== "build") {
  const destination = process.env.EMAIL_PREVIEW_DIR ??
    join(tmpdir(), "planarian-email-previews");
  mkdirSync(destination, { recursive: true });
  const render = Handlebars.compile(html);
  const websiteUrl = "https://planarian.example";
  const fixtures = {
    "password-reset": {
      header: "Reset your password",
      messages: ["We have received a request to reset your password for your account. If you did not make this request, please ignore this email. If you did make this request, please click the link below to reset your password. This link will expire in 30 minutes."],
      buttonText: "Reset Password",
      buttonUrl: websiteUrl + "/reset-password?token=example-1234567890"
    },
    "email-confirmation": {
      header: "Confirm your email address",
      messages: ["Welcome to Planarian!", "Please confirm your email address by clicking the link below. If you did not sign up for Planarian, please ignore this email."],
      buttonText: "Confirm Email",
      buttonUrl: websiteUrl + "/confirm?token=example-1234567890"
    },
    invitation: {
      header: "Welcome!",
      messages: ["You have been invited by Example Organization to join Planarian! Please click the link below to create your account and accept the invitation."],
      buttonText: "Create Account",
      buttonUrl: websiteUrl + "/invite?token=example-1234567890"
    },
    "access-disabled": {
      header: "Account access disabled",
      messages: ["Your access to Example Organization has been disabled.", "Contact your account administrator if you have questions."]
    },
    "membership-revoked": {
      header: "Membership revoked",
      messages: ["Your membership in Example Organization has been revoked."]
    },
    "password-changed": {
      header: "Password changed",
      messages: ["Your password was changed. If you did not make this change, please contact Example Support & Assistance immediately at help@example.org."]
    },
    "long-message": {
      header: "Account access disabled",
      messages: [
        "Your access to Example Organization has been disabled. This longer message checks how multiple sentences wrap inside the email across different devices.",
        "If you believe this is an error, contact your administrator and provide any relevant account details. Never provide passwords or one-time codes.",
        "Special characters should be escaped: <script>alert('example')</script> & other symbols."
      ]
    }
  };
  for (const [name, data] of Object.entries(fixtures)) {
    const result = render({ ...data, websiteUrl, previewText: data.messages[0] });
    assert.doesNotMatch(result, /{{[^}]+}}/, name + " has an unresolved placeholder");
    assert.equal(result.includes("If the button doesn't work"), Boolean(data.buttonText),
      name + " button fallback mismatch");
    if (data.buttonText) assert.ok(result.includes(data.buttonText), name + " missing button");
    if (name === "password-changed") {
      assert.ok(result.includes("Example Support &amp; Assistance immediately at help@example.org."),
        "Support details should be part of the password-change sentence");
      assert.ok(!result.includes("mailto:"), "No separate support contact block should be rendered");
    }
    writeFileSync(join(destination, name + ".html"), result);
    if (name === "long-message") {
      assert.ok(result.includes("&lt;script&gt;"), "Custom text must be escaped");
      assert.ok(!result.includes("<script>alert"), "Custom text must not inject HTML");
    }
  }
  console.log("Validated " + Object.keys(fixtures).length + " fully substituted email variants.");
  if (action === "preview") {
    const chrome = process.env.CHROME_PATH ??
      "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
    if (existsSync(chrome)) {
      const { default: puppeteer } = await import("puppeteer-core");
      const browser = await puppeteer.launch({ headless: true, executablePath: chrome });
      try {
        for (const name of Object.keys(fixtures)) {
          for (const [label, viewport] of [
            ["desktop", { width: 1000, height: 850 }],
            ["mobile", { width: 390, height: 800, isMobile: true, hasTouch: true }]
          ]) {
            const page = await browser.newPage();
            try {
              await page.setViewport({ ...viewport, deviceScaleFactor: 1 });
              await page.goto("file://" + join(destination, name + ".html"),
                { waitUntil: "load" });
              const overflows = await page.evaluate(() =>
                document.documentElement.scrollWidth > window.innerWidth);
              assert.equal(overflows, false, name + " overflows at " + label + " width");
              await page.screenshot({
                path: join(destination, name + "-" + label + ".png"),
                fullPage: true
              });
            } finally {
              await page.close();
            }
          }
        }
      } finally {
        await browser.close();
      }
      console.log("Saved Chrome screenshots for desktop and mobile.");
    } else {
      console.log("Chrome not available; generated HTML previews only. Set CHROME_PATH to enable screenshots.");
    }
  }
  console.log("Previews: " + destination);
}
