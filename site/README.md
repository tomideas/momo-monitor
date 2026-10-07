# Momo Monitor English guide

Open `index.html` directly in a browser. The guide works offline and can also be served as a static website. `getting-started.html` contains a five-step screenshot walkthrough. All labels, navigation and primary screenshots use the English interface.

The guide is generated from `dev/site/build-guide.py`. Its version comes from `StatusMonitor/StatusMonitor.csproj`. The shared CSS adapter comes from the current `design-system/design-system.json`; it never reads defaults. Fonts and the Momo mark are bundled locally from the application.

To update content or design tokens, run `python dev/site/build-guide.py` from the project root. Run `python dev/site/verify-content.py` and `node dev/site/verify-site.cjs` to validate content and browser behavior. The latter needs Playwright and a local Chrome installation. Verification reports and browser captures are in `dev/site/verification/`.

To refresh the screenshots, build the application, run `python dev/site/prepare-capture.py`, then build `dev/site/Capture/Capture.csproj` with `-p:OutputPath=bin\Docs\`. Run `dev/site/capture-guide.ps1` as administrator. It uses read-only render previews, reads the actual sensors through the existing driver, and does not save settings/history or send fan commands. Alert events are explicitly labelled examples. Driver-access warnings belong only in the troubleshooting illustrations.

`zh.html` and `docs.css`/`docs.js` retain the previous Chinese guide. They are not linked as a current translation of the new English documentation.
