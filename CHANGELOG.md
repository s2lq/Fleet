# Changelog

## 1.9.0

- layout rewritten: windows tile the screen exactly with nothing overlapping. rows are balanced instead of one wide leftover, the invisible window border windows has is measured so neighbours do not overlap, and there is no minimum size forced any more (windows are asked for the exact slot and only if roblox refuses is its smallest size remembered and the layout redone, spilling extra windows to other screens)
- every page can be scrolled all the way down now, and the accounts toolbar and tools tabs no longer run into each other on small windows
- tools > optimizer: swaps roblox's cursor for the normal windows pointer, replaces the font, turns the menu / emote wheel / top bar pictures black and white, and cleans old roblox versions, logs and cache. everything it changes is backed up and can be restored, and it can re-apply itself after roblox updates
- finds roblox when it is installed in program files, and the version list now includes the current version
- what's new window after an update, built from this changelog (also under settings > about)
- a short first launch tour that lights up each page, with a skip button. replay it from settings > about
- slimmer scrollbars that fade out, grow on hover and can be dragged
- right click menus and multiline boxes follow the theme
- fleet draws its own title bar with its own minimize, maximize and close buttons. dialogs have one too
- panels, popups and notifications are slightly see-through
- startup animation, and a nicer first setup: pick a theme and accent with a preview, steps slide in, the tour waits until you finish adding accounts
- smoother animations that run the same at any frame rate
- text boxes show the text cursor instead of the hand

## 1.8.1

- simpler layout page: windows fill the screen with no gaps and rearrange by themselves

## 1.8.0

- launching is faster. every login ticket is fetched at the same time, clients can all start at the same moment (new start all at once switch, on by default), and one shared detector finds all their windows instead of every account polling on its own. less disk writing during a launch
- windows arrange themselves: the best grid for the screen shape with no gaps, spread over all screens by size, and they rearrange when a window opens or closes
- fixed a bug where double clicking a word in a text field only selected part of it

## 1.7.0

- proper roblox version manager under settings > roblox: download any of the recent versions with progress and cancel, pick the default, delete old ones and see their sizes. accounts can have their own version too (edit account)
- scheduler under tools: launch a preset, launch everyone or stop everything at a time on chosen days
- fleet:// links and command line options. a second fleet hands its job to the first one
- gallery under tools: install shared configs and themes. the list lives in the gallery folder of the repo
- option to lower graphics when the crash breaker trips
- extra backup copies to a folder (cloud folder, usb stick), needs a password
- shortcuts: ctrl+f search, ctrl+l launch selected, ctrl+shift+s stop all, esc closes windows. there is a list in about
- open crash log button in about

## 1.6.0

- proper text fields: move the caret with the arrow keys, home, end and ctrl+arrows, select with shift or the mouse (double click picks a word), cut, copy, paste, undo, and a right-click menu
- typing in other languages works: japanese, chinese, korean and so on, including emoji, and the input window shows up next to the caret
- there are tests for the text fields now and they run in the build workflow

## 1.5.0

- animations are a lot lighter on the cpu now. the ui is cached and only redrawn when something changes, the wallpaper runs at a low frame rate, and everything pauses when fleet is minimized, in the background or on battery
- new animation setting in appearance: efficient (default), smooth or minimal
- the 3d viewer only renders when the model moves

## 1.4.0

- 3d avatar viewer. click an account's picture and fleet pulls the 3d model from roblox and shows it spinning. drag to turn it, scroll to zoom

## 1.3.0

- account tools under tools: summary, quick selection, sort, bulk rename, bulk edit, duplicate finder and export of the selected accounts

## 1.2.0

- account details window: age, premium, robux, friends and followers
- import accounts from other managers
- discord rich presence
- whole-pc memory cleaner, manual or automatic
- experimental proxy setting for the roblox process itself

## 1.1.0

- four light themes: paper, snow, sand and sky. the whole ui follows the theme

## 1.0.1

- animations: page slides, cards sliding in, counting numbers, toasts, pulsing dot on running accounts

## 1.0.0

first version.

- accounts as cards or a list with groups, tags, colours, notes, robux and mute. browser login or paste cookies
- fast overlapping launches, exact and private servers, follow a player, public server browser, game search
- saved private servers with a link check
- health page, event log, auto-relaunch and a crash breaker
- performance limits, quick and community configs, shareable configs
- window tiling, presets, macro recorder, mini window, compact mode, tray and hotkeys
- eight black themes, custom themes, ten languages, first-run setup
- proxies, discord alerts, roblox version changer, portable mode, backups, password protection and an updater
