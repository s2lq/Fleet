// sets up the fleet discord server. run it once, it is safe to run again (it skips what exists).
// usage: set DISCORD_TOKEN and GUILD_ID, then `node setup.js`
const { Client, GatewayIntentBits, ChannelType, PermissionFlagsBits: P } = require('discord.js');

const token = process.env.DISCORD_TOKEN;
const guildId = process.env.GUILD_ID;
if (!token || !guildId) {
  console.error('set DISCORD_TOKEN and GUILD_ID first (see README.md)');
  process.exit(1);
}

const roles = [
  { name: 'Owner', color: 0xffffff, hoist: true, permissions: [P.Administrator] },
  { name: 'Mod', color: 0xb0b0b0, hoist: true, permissions: [P.ManageMessages, P.KickMembers, P.BanMembers, P.ModerateMembers, P.ManageThreads] },
  { name: 'Tester', color: 0x808080, hoist: false, mentionable: true, permissions: [] },
  { name: 'Member', color: 0x555555, hoist: false, permissions: [] }
];

// readonly means only mods can post
const categories = [
  { name: 'info', channels: [
    { name: 'welcome', readonly: true, topic: 'rules and what Fleet is' },
    { name: 'announcements', readonly: true, topic: 'new releases and news' },
    { name: 'releases', readonly: true, topic: 'the github feed' },
    { name: 'faq', readonly: true, topic: 'common questions' }
  ] },
  { name: 'help', channels: [
    { name: 'help', topic: 'setup and usage questions' },
    { name: 'bugs', topic: 'bug reports. include the Activity log text and your Windows version' },
    { name: 'suggestions', topic: 'ideas' }
  ] },
  { name: 'community', channels: [
    { name: 'general' },
    { name: 'showcase', topic: 'screenshots of your setups and themes' },
    { name: 'configs', topic: 'share .fcfg configs and .ftheme themes' },
    { name: 'off-topic' }
  ] }
];

const welcome = [
  '**Welcome to Fleet**',
  'Fleet is a multi account manager and launcher for Roblox on Windows. Plain black UI, launch many accounts at once, windows arrange themselves.',
  '',
  '**Rules**',
  '1. Be decent to each other.',
  '2. Never post a cookie, a password or an account list. Anyone asking for one is a scammer.',
  '3. No selling accounts or Robux.',
  '4. No help with breaking Roblox\'s rules beyond what Fleet does itself.',
  '5. Bugs go in #bugs with details, not in #general.'
].join('\n');

const faq = [
  '**Is Fleet safe?**',
  'Accounts are encrypted on your PC and only sent to Roblox. The code is public.',
  '',
  '**Roblox says I\'m not allowed to run several clients.**',
  'Using several clients may be against Roblox\'s rules. It is at your own risk.',
  '',
  '**Windows warns about the exe.**',
  'It isn\'t code signed yet. Check the SHA-256 in the release.',
  '',
  '**Where is my data?**',
  '`%AppData%\\Fleet`, or `FleetData` next to the exe in portable mode.'
].join('\n');

async function main(client) {
  const guild = await client.guilds.fetch(guildId);
  await guild.roles.fetch();
  await guild.channels.fetch();

  const made = {};
  for (const r of roles) {
    let role = guild.roles.cache.find(x => x.name === r.name);
    if (!role) {
      role = await guild.roles.create({ name: r.name, color: r.color, hoist: !!r.hoist, mentionable: !!r.mentionable, permissions: r.permissions, reason: 'fleet setup' });
      console.log('role', r.name);
    }
    made[r.name] = role;
  }

  const everyone = guild.roles.everyone;
  for (const c of categories) {
    let cat = guild.channels.cache.find(x => x.type === ChannelType.GuildCategory && x.name.toLowerCase() === c.name);
    if (!cat) {
      cat = await guild.channels.create({ name: c.name, type: ChannelType.GuildCategory, reason: 'fleet setup' });
      console.log('category', c.name);
    }
    for (const ch of c.channels) {
      let chan = guild.channels.cache.find(x => x.type === ChannelType.GuildText && x.name === ch.name);
      if (!chan) {
        const overwrites = [];
        if (ch.readonly) {
          overwrites.push({ id: everyone.id, deny: [P.SendMessages, P.AddReactions, P.CreatePublicThreads] });
          overwrites.push({ id: made.Mod.id, allow: [P.SendMessages] });
        }
        chan = await guild.channels.create({ name: ch.name, type: ChannelType.GuildText, parent: cat.id, topic: ch.topic, permissionOverwrites: overwrites, reason: 'fleet setup' });
        console.log('channel', ch.name);
      }
      made['#' + ch.name] = chan;
    }
  }

  await post(made['#welcome'], welcome);
  await post(made['#faq'], faq);

  // webhook for the github feed
  const rel = made['#releases'];
  const hooks = await rel.fetchWebhooks();
  let hook = hooks.find(h => h.name === 'Fleet releases');
  if (!hook) {
    hook = await rel.createWebhook({ name: 'Fleet releases' });
    console.log('webhook created');
  }
  console.log('');
  console.log('release webhook url (keep it private, use it as the DISCORD_WEBHOOK repo secret):');
  console.log(hook.url);
  console.log('for the github feed, add the same url with /github on the end under repo Settings > Webhooks.');
}

// only posts if the channel has no bot message yet, so reruns do not spam
async function post(chan, text) {
  const msgs = await chan.messages.fetch({ limit: 20 });
  if (msgs.some(m => m.author.id === chan.client.user.id)) return;
  const m = await chan.send(text);
  try { await m.pin(); } catch (e) { }
  console.log('posted in #' + chan.name);
}

const client = new Client({ intents: [GatewayIntentBits.Guilds] });
client.once('ready', async () => {
  try { await main(client); console.log('done'); }
  catch (e) { console.error(e.message || e); process.exitCode = 1; }
  client.destroy();
});
client.login(token);
