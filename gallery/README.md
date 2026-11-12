# Gallery

Configs (`.fcfg`) and themes (`.ftheme`) that show up in Fleet under Tools > Gallery.

To add yours, export it from Fleet, drop the file in this folder, add an entry to `index.json` and open a pull request.
An entry looks like this:

```json
{ "type": "config", "name": "My setup", "by": "you", "about": "One line about it.", "file": "my-setup.fcfg" }
```

Use `"type": "theme"` for `.ftheme` files. Never include cookies, passwords or private links: configs only hold settings.
