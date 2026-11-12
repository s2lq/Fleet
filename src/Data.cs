using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Fleet
{
    public class Account
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string RobloxVersion = "";     // blank = the default version
        public string Name = "";
        public string Display = "";
        public string Alias = "";
        public long UserId;
        public string Cookie = "";
        public string Password = "";
        public string Note = "";
        public string Group = "";
        public string Tags = "";
        public string Color = "";
        public string Place = "";
        public string PrivateLink = "";
        public string ServerId = "";
        public string Proxy = "";
        public bool ProxyAuto;
        public bool Muted;
        public int FpsCap;
        public long Robux = -1;
        public bool Premium;
        public string Created = "";
        public int Friends = -1;
        public int Followers = -1;
        public List<string> FriendNames = new List<string>();
        public long DetailsTicks;
        public int Status;
        public long LastCheck;
        public long LastLaunch;
        public int Order;

        public string Label { get { return !string.IsNullOrEmpty(Alias) ? Alias : (string.IsNullOrEmpty(Display) ? Name : Display); } }

        public List<string> TagList()
        {
            return (Tags ?? "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        }
    }

    public class SavedServer
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "";
        public long Place;
        public string Code = "";
        public string Link = "";
        public string Owner = "";
        public int Check;
        public string CheckText = "";
    }

    public class Preset
    {
        public string Name = "";
        public List<string> AccountIds = new List<string>();
        public string Mode = "Game";
        public string Place = "";
        public string Job = "";
        public string Private = "";
        public int Cols;
        public int Gap = 6;
    }

    public class ScheduleItem
    {
        public string Name = "";
        public string Time = "20:00";          // HH:mm
        public int Days = 127;                 // bit 0 = Monday ... bit 6 = Sunday
        public string Action = "Launch preset"; // Launch preset | Launch all | Stop all
        public string Preset = "";
        public bool Enabled = true;
        public string LastRun = "";
    }

    public class CustomTheme { public string Name = ""; public string Bg = "#000000", Panel = "#0B0B0B", Line = "#222222", Text = "#F0F0F0", Muted = "#969696"; }

    public class Settings
    {
        public int Accent = 0;
        public string Theme = "Black";
        public List<CustomTheme> CustomThemes = new List<CustomTheme>();
        public string Wallpaper = "Dots";
        public bool ReduceMotion = false;
        public int Motion = 0;                  // 0 efficient, 1 smooth, 2 minimal
        public bool PowerSave = true;           // quiet the animations on battery
        public string Language = "en";
        public bool Compact = false;
        public int MiniX = -1, MiniY = -1;

        public bool MultiInstance = true;
        public string LaunchMethod = "Protocol";
        public int StaggerMs = 700;
        public bool UseOwn = true;
        public string LaunchMode = "Game";
        public string LastPlace = "";
        public string LastJob = "";
        public string LastPrivate = "";
        public string LastFollow = "";
        public bool OnlySelected = true;
        public bool RefreshCookie = false;
        public string RobloxVersion = "";

        public int FpsCap = 0;
        public string Priority = "Normal";
        public bool AutoTrim = false;
        public int TrimEveryMin = 10;
        public string CpuCapMode = "Off";
        public int CpuCapPercent = 10;
        public string RamCapMode = "Off";
        public bool ThrottleBackground = false;
        public int BackgroundFps = 10;
        public string GfxPreset = "Default";
        public bool AntiAfk = false;
        public int AntiAfkMin = 12;

        public bool AutoRelaunch = false;
        public int WatcherSec = 60;
        public bool RejoinSame = true;
        public bool Breaker = true;
        public int BreakerMax = 3;
        public int BreakerCooldownMin = 10;

        public string ProxyPool = "";
        public int ProxyPerAccounts = 1;
        public bool ProxyAuto = false;

        public string Webhook = "";
        public bool HookCrash = true, HookRelaunch = true, HookLocked = true, HookBatch = false;

        public bool HealthAuto = true;
        public int HealthHours = 6;

        public bool Tray = true;
        public bool CloseToTray = false;
        public bool MinToTray = false;
        public bool Toasts = true;
        public bool Hotkeys = true;
        public bool StartWithWindows = false;

        public bool CheckUpdates = true;
        public List<ScheduleItem> Schedule = new List<ScheduleItem>();
        public bool LowerGfxOnCrash = false;
        public string BackupFolder = "";
        public bool UrlHandler = false;
        public bool RichPresence = false;
        public string DiscordAppId = "";
        public bool SysMemAuto = false;
        public int SysMemThreshold = 80;
        public bool GameProxyEnv = false;
        public bool AutoInstall = false;

        public int LayoutMonitor = 0;
        public int LayoutGap = 6;
        public int LayoutCols = 0;
        public bool LayoutBorderless = false;
        public bool LayoutTopMost = false;
        public bool LayoutRename = false;

        public bool FirstRunDone = false;
        public string StartupPreset = "";
        public int MacroLoops = 1;
        public List<string> Recent = new List<string>();
        public string LastBackup = "";
    }

    public class JournalEntry { public long Ticks; public string Account = ""; public string Kind = ""; public string Text = ""; }

    public static class Vault
    {
        public static byte[] Encrypt(byte[] plain, string password)
        {
            byte[] salt = new byte[16], iv = new byte[16];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) { rng.GetBytes(salt); rng.GetBytes(iv); }
            byte[] ek, mk;
            Keys(password, salt, out ek, out mk);
            byte[] cipher;
            using (Aes aes = Aes.Create())
            {
                aes.Key = ek; aes.IV = iv;
                using (ICryptoTransform t = aes.CreateEncryptor()) cipher = t.TransformFinalBlock(plain, 0, plain.Length);
            }
            byte[] body = salt.Concat(iv).Concat(cipher).ToArray();
            byte[] mac;
            using (HMACSHA256 h = new HMACSHA256(mk)) mac = h.ComputeHash(body);
            return Encoding.ASCII.GetBytes("FLT1").Concat(body).Concat(mac).ToArray();
        }

        public static byte[] Decrypt(byte[] blob, string password)
        {
            if (blob.Length < 4 + 32 + 32 || Encoding.ASCII.GetString(blob, 0, 4) != "FLT1") throw new InvalidDataException("Not a Fleet file.");
            byte[] body = blob.Skip(4).Take(blob.Length - 4 - 32).ToArray();
            byte[] mac = blob.Skip(blob.Length - 32).ToArray();
            byte[] ek, mk;
            Keys(password, body.Take(16).ToArray(), out ek, out mk);
            byte[] check;
            using (HMACSHA256 h = new HMACSHA256(mk)) check = h.ComputeHash(body);
            bool same = check.Length == mac.Length;
            for (int i = 0; i < check.Length && i < mac.Length; i++) if (check[i] != mac[i]) same = false;
            if (!same) throw new UnauthorizedAccessException("Wrong password.");
            using (Aes aes = Aes.Create())
            {
                aes.Key = ek; aes.IV = body.Skip(16).Take(16).ToArray();
                byte[] cipher = body.Skip(32).ToArray();
                using (ICryptoTransform t = aes.CreateDecryptor()) return t.TransformFinalBlock(cipher, 0, cipher.Length);
            }
        }

        public static bool IsVault(byte[] blob) { return blob.Length > 4 && Encoding.ASCII.GetString(blob, 0, 4) == "FLT1"; }

        static void Keys(string pw, byte[] salt, out byte[] enc, out byte[] mac)
        {
            using (Rfc2898DeriveBytes k = new Rfc2898DeriveBytes(pw, salt, 60000))
            {
                byte[] all = k.GetBytes(64);
                enc = all.Take(32).ToArray(); mac = all.Skip(32).ToArray();
            }
        }
    }

    public static class Store
    {
        public static string Dir;
        public static List<Account> Accounts = new List<Account>();
        public static List<SavedServer> Servers = new List<SavedServer>();
        public static List<Preset> Presets = new List<Preset>();
        public static List<JournalEntry> Journal = new List<JournalEntry>();
        public static Settings Cfg = new Settings();
        public static string Password;
        public static bool Locked;
        static readonly JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        static readonly byte[] Salt = Encoding.UTF8.GetBytes("fleet.accounts.v1");

        public static string ExeDir { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public static string PortableMarker { get { return Path.Combine(ExeDir, "portable.txt"); } }
        public static string PortableDir { get { return Path.Combine(ExeDir, "FleetData"); } }
        public static string NormalDir
        {
            get
            {
                string b = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(b)) b = Path.GetTempPath();
                return Path.Combine(b, "Fleet");
            }
        }
        public static bool IsPortable { get { return File.Exists(PortableMarker); } }

        public static void Init()
        {
            Dir = IsPortable ? PortableDir : NormalDir;
            Directory.CreateDirectory(Dir);
            LoadSettings();
            LoadSide();
        }

        static string P(string n) { return Path.Combine(Dir, n); }
        static bool Windows { get { return Environment.OSVersion.Platform == PlatformID.Win32NT; } }

        public static void LoadSettings()
        {
            try { if (File.Exists(P("settings.json"))) Cfg = js.Deserialize<Settings>(File.ReadAllText(P("settings.json"), Encoding.UTF8)) ?? new Settings(); }
            catch (Exception) { Cfg = new Settings(); }
            if (Cfg.Wallpaper != "Grid" && Cfg.Wallpaper != "Off") Cfg.Wallpaper = "Dots";
        }

        public static void SaveSettings()
        {
            try { Write(P("settings.json"), Encoding.UTF8.GetBytes(js.Serialize(Cfg))); }
            catch (Exception ex) { Log.Add("Could not save settings: " + ex.Message); }
        }

        static void LoadSide()
        {
            try { if (File.Exists(P("presets.json"))) Presets = js.Deserialize<List<Preset>>(File.ReadAllText(P("presets.json"), Encoding.UTF8)) ?? new List<Preset>(); } catch (Exception) { }
            try { if (File.Exists(P("events.json"))) Journal = js.Deserialize<List<JournalEntry>>(File.ReadAllText(P("events.json"), Encoding.UTF8)) ?? new List<JournalEntry>(); } catch (Exception) { }
        }

        public static void SavePresets() { try { Write(P("presets.json"), Encoding.UTF8.GetBytes(js.Serialize(Presets))); } catch (Exception) { } }
        public static void SaveJournal()
        {
            if (Journal.Count > 1500) Journal.RemoveRange(0, Journal.Count - 1000);
            try { Write(P("events.json"), Encoding.UTF8.GetBytes(js.Serialize(Journal))); } catch (Exception) { }
        }

        class Box { public List<Account> Accounts = new List<Account>(); public List<SavedServer> Servers = new List<SavedServer>(); }

        public static bool AccountsPasswordProtected()
        {
            try { return File.Exists(P("accounts.fdat")) && Vault.IsVault(File.ReadAllBytes(P("accounts.fdat"))); } catch (Exception) { return false; }
        }

        // false = wrong password (or unreadable)
        public static bool LoadAccounts(string password)
        {
            try
            {
                if (!File.Exists(P("accounts.fdat"))) { Locked = false; return true; }
                byte[] raw = File.ReadAllBytes(P("accounts.fdat"));
                byte[] plain;
                if (Vault.IsVault(raw)) { if (password == null) return false; plain = Vault.Decrypt(raw, password); Password = password; }
                else plain = Windows ? ProtectedData.Unprotect(raw, Salt, DataProtectionScope.CurrentUser) : raw;
                string json = Encoding.UTF8.GetString(plain);
                Box b = json.TrimStart().StartsWith("[") ? new Box { Accounts = js.Deserialize<List<Account>>(json) } : js.Deserialize<Box>(json);
                Accounts = b.Accounts ?? new List<Account>();
                Servers = b.Servers ?? new List<SavedServer>();
                Locked = false;
                return true;
            }
            catch (Exception ex)
            {
                Log.Add("Could not open the accounts file: " + ex.Message);
                return false;
            }
        }

        public static void SaveAccounts()
        {
            try
            {
                byte[] plain = Encoding.UTF8.GetBytes(js.Serialize(new Box { Accounts = Accounts, Servers = Servers }));
                byte[] blob = Password != null ? Vault.Encrypt(plain, Password) : (Windows ? ProtectedData.Protect(plain, Salt, DataProtectionScope.CurrentUser) : plain);
                Write(P("accounts.fdat"), blob);
            }
            catch (Exception ex) { Log.Add("Could not save accounts: " + ex.Message); }
        }

        static void Write(string path, byte[] bytes)
        {
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public static Account Find(string id) { return Accounts.FirstOrDefault(a => a.Id == id); }
        public static SavedServer FindServer(string id) { return Servers.FirstOrDefault(s => s.Id == id); }

        public static List<string> Groups() { return Accounts.Select(a => a.Group).Where(g => !string.IsNullOrEmpty(g)).Distinct().OrderBy(g => g).ToList(); }
        public static List<string> AllTags() { return Accounts.SelectMany(a => a.TagList()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToList(); }

        // ---------- backups
        public static string Backup()
        {
            try
            {
                string src = P("accounts.fdat");
                if (!File.Exists(src)) return null;
                string dir = P("Backups");
                Directory.CreateDirectory(dir);
                string dst = Path.Combine(dir, "accounts-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".fdat");
                File.Copy(src, dst, true);
                CopyOut(dst);
                foreach (string old in Directory.GetFiles(dir, "accounts-*.fdat").OrderByDescending(x => x).Skip(12)) File.Delete(old);
                return dst;
            }
            catch (Exception) { return null; }
        }

        // an extra copy in a folder of your choice (a synced cloud folder, a USB stick); only with a password, otherwise it could not be read elsewhere
        static void CopyOut(string file)
        {
            string f = Cfg.BackupFolder;
            if (string.IsNullOrEmpty(f) || !Directory.Exists(f)) return;
            if (Password == null) { Log.Add("Backup copy skipped: set a password first, Windows-encrypted accounts cannot be opened on another PC."); return; }
            try { File.Copy(file, Path.Combine(f, "fleet-" + Path.GetFileName(file)), true); }
            catch (Exception ex) { Log.Add("Could not copy the backup: " + ex.Message); }
        }

        public static void AutoBackup()
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            if (Cfg.LastBackup == today) return;
            if (Backup() != null) { Cfg.LastBackup = today; SaveSettings(); }
        }

        public static string RestoreFrom(string file)
        {
            try
            {
                byte[] raw = File.ReadAllBytes(file);
                if (Vault.IsVault(raw) && Password == null) return "That backup is password protected. Set the same password first.";
                Backup();
                File.Copy(file, P("accounts.fdat"), true);
                return LoadAccounts(Password) ? null : "The backup could not be read.";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ---------- portable mode
        public static string SetPortable(bool on)
        {
            try
            {
                string from = Dir, to = on ? PortableDir : NormalDir;
                CopyDir(from, to);
                if (on) File.WriteAllText(PortableMarker, "Fleet keeps its data in FleetData next to the exe while this file exists.");
                else if (File.Exists(PortableMarker)) File.Delete(PortableMarker);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        static void CopyDir(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
            foreach (string d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
        }

        // ---------- move to another PC
        public static void ExportTransfer(string file, string password)
        {
            byte[] plain = Encoding.UTF8.GetBytes(js.Serialize(new Box { Accounts = Accounts, Servers = Servers }));
            File.WriteAllBytes(file, Vault.Encrypt(plain, password));
        }

        public static int ImportTransfer(string file, string password)
        {
            byte[] plain = Vault.Decrypt(File.ReadAllBytes(file), password);
            Box b = js.Deserialize<Box>(Encoding.UTF8.GetString(plain));
            int added = 0;
            foreach (Account a in b.Accounts ?? new List<Account>())
            {
                Account ex = Accounts.FirstOrDefault(x => x.UserId == a.UserId && a.UserId > 0);
                if (ex != null) { ex.Cookie = a.Cookie; continue; }
                a.Order = Accounts.Count; Accounts.Add(a); added++;
            }
            foreach (SavedServer s in b.Servers ?? new List<SavedServer>())
                if (!Servers.Any(x => x.Id == s.Id)) Servers.Add(s);
            SaveAccounts();
            return added;
        }
    }

    public static class Log
    {
        public class Entry { public DateTime At; public string Text; public int Id; }
        public static int Total;
        public static readonly List<Entry> Lines = new List<Entry>();
        public static event Action Changed;

        public static void Add(string text)
        {
            lock (Lines)
            {
                Lines.Add(new Entry { At = DateTime.Now, Text = text, Id = ++Total });
                if (Lines.Count > 400) Lines.RemoveRange(0, 100);
            }
            Action h = Changed;
            if (h != null) h();
        }
    }

    // saved history of what happened to which account
    public static class Journal2
    {
        public static void Add(Account a, string kind, string text)
        {
            Store.Journal.Add(new JournalEntry { Ticks = DateTime.UtcNow.Ticks, Account = a != null ? a.Label : "", Kind = kind, Text = text });
            Store.SaveJournal();
        }

        public static int CrashesLast7(Account a)
        {
            long cut = DateTime.UtcNow.AddDays(-7).Ticks;
            return Store.Journal.Count(e => e.Account == a.Label && e.Ticks > cut && (e.Kind == "Crash" || e.Kind == "Kicked"));
        }

        public static void ExportCsv(string file)
        {
            StringBuilder sb = new StringBuilder("time,account,kind,text\r\n");
            foreach (JournalEntry e in Store.Journal)
                sb.Append(new DateTime(e.Ticks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")).Append(',').Append(Csv(e.Account)).Append(',').Append(e.Kind).Append(',').Append(Csv(e.Text)).Append("\r\n");
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        }

        static string Csv(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }
    }
}
