# Server setup bot

Sets up the Fleet Discord server for you: roles, categories, channels, read only permissions, the rules and FAQ posts, and a webhook for the releases feed. Running it again skips whatever already exists.

## Steps

1. Make an empty server in Discord.
2. Open the Developer Portal, New Application, then Bot. Reset the token and copy it.
3. Under OAuth2 > URL Generator pick `bot` and the permission Administrator, open the link and add the bot to your server.
4. Copy the server id (turn on Developer Mode in Discord settings, right click the server, Copy Server ID).
5. Run it:

```bash
cd discord-bot
npm install
```

```bash
DISCORD_TOKEN=your-token GUILD_ID=your-server-id node setup.js
```

On Windows PowerShell use `$env:DISCORD_TOKEN="your-token"; $env:GUILD_ID="your-server-id"; node setup.js`.

At the end it prints a webhook url for #releases. Put that in the repo as the `DISCORD_WEBHOOK` secret so releases post themselves. Move the Owner role above the bot's role and give it to yourself, then kick the bot.

The token never goes in a file. Do not commit it.
