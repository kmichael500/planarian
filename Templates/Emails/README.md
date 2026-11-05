# Transactional email template

Edit `GenericEmailTemplate.mjml`, then regenerate the checked-in `GenericEmailTemplate.html`:

```sh
npm ci
npm run build
npm run check
```

`npm run check` validates the template and sample messages. `npm run preview` also renders desktop and mobile screenshots when Chrome is available. Previews are written outside the repository.

The API embeds the generated HTML and fills it with Handlebars substitutions. Template changes require a new API deployment.

The sender is `Planarian` at `notifications@{Email:FromDomain}` when the optional override is set, or `notifications@{Email:Domain}` otherwise.
