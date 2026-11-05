# Planarian email template

`GenericEmailTemplate.mjml` is the source for all Planarian transactional emails.
`GenericEmailTemplate.html` is generated from it and embedded in the backend build.
The API uses the compiled HTML directly. All generic emails are sent from
Planarian (notifications@{Email:FromDomain}) when the optional sender-domain
override is configured, otherwise notifications@{Email:Domain}.

From this directory:

```sh
npm ci
npm run build
npm run check
npm run preview
```

`build` regenerates the checked-in HTML. `check` validates MJML, checks
the generated HTML is current, and renders seven sample messages through
Handlebars. `preview` also captures desktop and mobile screenshots when
Chrome is available (set `CHROME_PATH` for another executable). Previews are
written to a temporary directory, not the repository.

The API applies substitutions using Handlebars.Net; the local preview script
uses JavaScript Handlebars. The backend tests verify the embedded HTML with
Handlebars.Net as well. Neither preview nor test sends email.

The generic template is deployed with the API, not through a database edit.
Changing its source requires running `build` and deploying the new API.

Password-change notifications include Server:SupportName and
Server:SupportEmail directly in the message sentence. Organization-access
notices preserve the administrator-provided message and do not add support details.
