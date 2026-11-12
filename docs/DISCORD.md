# Discord server setup

A plan for the Fleet server. Make it in Discord (Add a Server, Create My Own, For a club or community), then paste the invite link into the README.

## Channels

**info**
- `#welcome` rules and what Fleet is, read only
- `#announcements` new releases, read only
- `#releases` the GitHub feed (see below), read only
- `#faq` the common questions, read only

**help**
- `#help` setup and usage questions
- `#bugs` bug reports (ask for the Activity log text and your Windows version)
- `#suggestions` ideas

**community**
- `#general`
- `#showcase` screenshots of your setups and themes
- `#configs` share `.fcfg` configs and `.ftheme` themes
- `#off-topic`

## Roles

- Owner
- Mod
- Tester (gets pinged for beta builds)
- Member

## Rules to pin in #welcome

1. Be decent to each other.
2. Never post a cookie, a password or an account list. Anyone asking for one is a scammer.
3. No selling accounts or Robux.
4. No help with breaking Roblox's rules beyond what Fleet does itself.
5. Bugs go in #bugs with details, not in #general.

## FAQ to pin

- Is Fleet safe? Accounts are encrypted on your PC and only sent to Roblox. The code is public.
- Roblox says I'm not allowed to run several clients. Using several clients may be against Roblox's rules. It's at your own risk.
- Windows warns about the exe. It isn't code signed yet. Check the SHA-256 in the release.
- Where is my data? `%AppData%\Fleet`, or `FleetData` next to the exe in portable mode.

## Feeding releases and commits into Discord

1. In the channel settings open Integrations, then Webhooks, then New Webhook, and copy its URL.
2. In the GitHub repo open Settings, Webhooks, Add webhook. Paste the URL and add `/github` on the end. Set the content type to `application/json` and pick the events you want (push, releases, issues).
3. For the Release workflow to post by itself, add that URL (without `/github`) as a repository secret called `DISCORD_WEBHOOK`.

## Safety settings

- Verification level: Medium.
- Turn on the explicit media filter.
- Turn off "allow members to send links" in #welcome and #announcements.
- Use two-factor for moderators.
