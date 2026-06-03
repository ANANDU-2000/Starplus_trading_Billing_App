# POS PDF on Honor / Android Tablet (PWA)

## After each deploy — client must refresh

1. Open the site in **Chrome** (not an old home-screen shortcut first).
2. Check **login footer** or POS success box: it must show **today's App build** date/time.
3. If you still see **"Print Receipt"** or **"Download started — check downloads folder"**, you are on an **old cache**:
   - Remove the app from the home screen (uninstall PWA).
   - Chrome → Site settings → **Clear data** / Storage → Clear.
   - Open `https://starplustrading.netlify.app` again.
   - If an **amber "Update now"** banner appears, tap it.
   - Re-add to home screen if needed.

## POS after creating a sale

The success popup shows the **real invoice PDF inside the popup** (iframe). Wait until you see the tax invoice — then use buttons.

| Button | What it does |
|--------|----------------|
| **View Invoice PDF** | Opens full-screen PDF viewer |
| **Print Invoice PDF** | Prints the PDF shown in the popup (only works after PDF loads) |
| **Save Invoice PDF** | Saves only when Share/Files or folder picker succeeds — no fake “download started” message |

If you still see **Print Receipt** or **Download PDF**, you are on an old app — the blocking **Update app now** screen must appear after deploy; clear cache if it does not.

Do **not** use the browser menu Print (Ctrl+P) on the POS page — that prints a blank screen.

## Troubleshooting

- **Excel or table opens instead of PDF**: Old app version — clear cache (steps above).
- **PDF preview empty**: Wait a few seconds (PDF loads from server); check internet and API.
- **Save does nothing on tablet**: Tap **Save to device** → Android Share sheet → **Save to Files** or **Downloads**.

## Build verification

Developers: `dist/version.json` is generated on each Netlify build. The app compares it to the embedded build and shows **Update now** when they differ.
