using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Fleet
{
    public class Material { public int W, H; public int[] Pix; public int Color = unchecked((int)0xFFC8C8C8); }

    public class Mesh
    {
        public float[] X, Y, Z, U, V;         // three entries per triangle
        public int[] Mat;                      // material per triangle
        public List<Material> Mats = new List<Material>();
        public float[] Center = new float[3], Eye = new float[3], Dir = new float[] { 0, 0, -1 };
        public float Fov = 70f;
        public int Triangles { get { return Mat == null ? 0 : Mat.Length; } }

        // a blocky stand-in figure, used for tests
        public static Mesh Demo()
        {
            List<float[]> verts = new List<float[]>();
            List<int> mats = new List<int>();
            Mesh m = new Mesh();
            int[] cols = { unchecked((int)0xFFF2C94C), unchecked((int)0xFF3B82F6), unchecked((int)0xFFE5E7EB), unchecked((int)0xFF22C55E) };
            foreach (int c in cols) m.Mats.Add(new Material { Color = c });
            Action<float, float, float, float, float, float, int> box = delegate(float x, float y, float z, float w, float h, float d, int mat)
            {
                float[][] p = {
                    new float[]{x-w,y-h,z-d}, new float[]{x+w,y-h,z-d}, new float[]{x+w,y+h,z-d}, new float[]{x-w,y+h,z-d},
                    new float[]{x-w,y-h,z+d}, new float[]{x+w,y-h,z+d}, new float[]{x+w,y+h,z+d}, new float[]{x-w,y+h,z+d} };
                int[][] f = { new[]{0,1,2,3}, new[]{5,4,7,6}, new[]{4,0,3,7}, new[]{1,5,6,2}, new[]{3,2,6,7}, new[]{4,5,1,0} };
                foreach (int[] q in f)
                {
                    int[][] t = { new[]{q[0],q[1],q[2]}, new[]{q[0],q[2],q[3]} };
                    foreach (int[] tri in t) { foreach (int i in tri) verts.Add(new float[]{ p[i][0], p[i][1], p[i][2], 0, 0 }); mats.Add(mat); }
                }
            };
            box(0, 2.6f, 0, 0.7f, 0.7f, 0.7f, 0);      // head
            box(0, 1.0f, 0, 1.0f, 1.0f, 0.5f, 1);      // torso
            box(-1.5f, 1.0f, 0, 0.5f, 1.0f, 0.5f, 0);  // arms
            box(1.5f, 1.0f, 0, 0.5f, 1.0f, 0.5f, 0);
            box(-0.5f, -1.0f, 0, 0.5f, 1.0f, 0.5f, 3); // legs
            box(0.5f, -1.0f, 0, 0.5f, 1.0f, 0.5f, 3);
            int n = verts.Count;
            m.X = new float[n]; m.Y = new float[n]; m.Z = new float[n]; m.U = new float[n]; m.V = new float[n]; m.Mat = mats.ToArray();
            for (int i = 0; i < n; i++) { m.X[i] = verts[i][0]; m.Y[i] = verts[i][1]; m.Z[i] = verts[i][2]; }
            m.Center = new float[] { 0, 0.8f, 0 };
            m.Eye = new float[] { 0, 1.6f, 11 };
            m.Dir = new float[] { 0, -0.07f, -1 };
            return m;
        }
    }

    // downloads Roblox's own 3D avatar model (OBJ + MTL + textures) for an account
    public static class Avatar3D
    {
        static readonly JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        static string CacheDir { get { string d = Path.Combine(Store.Dir, "Avatar3D"); Directory.CreateDirectory(d); return d; } }

        static string Host(string hash)
        {
            int i = 31;
            foreach (char c in hash) i ^= c;
            return "https://t" + (i % 8) + ".rbxcdn.com/" + hash;
        }

        static async Task<byte[]> File(string hash, string proxy)
        {
            string path = Path.Combine(CacheDir, hash);
            if (System.IO.File.Exists(path)) return System.IO.File.ReadAllBytes(path);
            byte[] data = await Roblox.GetBytes(Host(hash), proxy);
            try { System.IO.File.WriteAllBytes(path, data); } catch (Exception) { }
            return data;
        }

        static float F(object o) { return Convert.ToSingle(o, CultureInfo.InvariantCulture); }
        static float[] Vec(object o) { Dictionary<string, object> d = (Dictionary<string, object>)o; return new float[] { F(d["x"]), F(d["y"]), F(d["z"]) }; }

        public static async Task<Mesh> Load(Account a, Action<string> status)
        {
            status("Asking Roblox for the 3D avatar...");
            string url = "https://thumbnails.roblox.com/v1/users/avatar-3d?userId=" + a.UserId;
            string meta = null;
            for (int i = 0; i < 8 && meta == null; i++)
            {
                Dictionary<string, object> r = js.Deserialize<Dictionary<string, object>>(Encoding.GetString(await Roblox.GetBytes(url, a.Proxy)));
                string state = Convert.ToString(r["state"]);
                if (state == "Completed" && r.ContainsKey("imageUrl") && r["imageUrl"] != null) meta = Convert.ToString(r["imageUrl"]);
                else if (state == "Blocked" || state == "Error" || state == "InReview") throw new InvalidOperationException("Roblox has no 3D avatar for this account (" + state + ").");
                else { status("Roblox is building the model..."); await Task.Delay(1500); }
            }
            if (meta == null) throw new TimeoutException("Roblox did not finish the 3D avatar in time.");

            status("Downloading the model...");
            Dictionary<string, object> m = js.Deserialize<Dictionary<string, object>>(Encoding.GetString(await Roblox.GetBytes(meta, a.Proxy)));
            string objHash = Convert.ToString(m["obj"]), mtlHash = Convert.ToString(m["mtl"]);
            string obj = Encoding.GetString(await File(objHash, a.Proxy)), mtl = Encoding.GetString(await File(mtlHash, a.Proxy));
            List<string> texHashes = new List<string>();
            if (m.ContainsKey("textures")) foreach (object t in (System.Collections.ArrayList)m["textures"]) texHashes.Add(Convert.ToString(t));
            Dictionary<string, Bitmap> tex = new Dictionary<string, Bitmap>();
            int n = 0;
            foreach (string h in texHashes)
            {
                status("Downloading textures " + (++n) + "/" + texHashes.Count + "...");
                byte[] png = await File(h, a.Proxy);
                using (MemoryStream ms = new MemoryStream(png)) tex[h] = new Bitmap(ms);
            }
            status("Building the model...");
            Mesh mesh = Parse(obj, mtl, tex);
            if (m.ContainsKey("aabb"))
            {
                Dictionary<string, object> bb = (Dictionary<string, object>)m["aabb"];
                float[] mn = Vec(bb["min"]), mx = Vec(bb["max"]);
                mesh.Center = new float[] { (mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, (mn[2] + mx[2]) / 2 };
            }
            if (m.ContainsKey("camera"))
            {
                Dictionary<string, object> cam = (Dictionary<string, object>)m["camera"];
                mesh.Eye = Vec(cam["position"]);
                if (cam.ContainsKey("direction")) mesh.Dir = Vec(cam["direction"]);
                if (cam.ContainsKey("fov")) mesh.Fov = F(cam["fov"]);
            }
            return mesh;
        }

        static readonly System.Text.Encoding Encoding = System.Text.Encoding.UTF8;

        static Mesh Parse(string obj, string mtl, Dictionary<string, Bitmap> tex)
        {
            // materials: name -> texture hash or colour
            Dictionary<string, int> matIndex = new Dictionary<string, int>();
            Mesh mesh = new Mesh();
            string cur = null; Material curMat = null;
            Dictionary<string, Material> byName = new Dictionary<string, Material>();
            foreach (string raw in mtl.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("newmtl ")) { cur = line.Substring(7).Trim(); curMat = new Material(); byName[cur] = curMat; }
                else if (curMat != null && line.StartsWith("map_Kd "))
                {
                    string name = line.Substring(7).Trim();
                    name = name.Substring(name.LastIndexOfAny(new char[] { '/', '\\' }) + 1);
                    Bitmap b;
                    if (!tex.TryGetValue(name, out b) && tex.Count == 1) foreach (Bitmap only in tex.Values) b = only;
                    if (b != null) Fill(curMat, b);
                }
                else if (curMat != null && line.StartsWith("Kd "))
                {
                    string[] p = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 4) curMat.Color = unchecked((int)(0xFF000000 | (uint)(Clamp(p[1]) << 16) | (uint)(Clamp(p[2]) << 8) | (uint)Clamp(p[3])));
                }
            }
            List<float[]> pos = new List<float[]>(), uv = new List<float[]>();
            List<float> X = new List<float>(), Y = new List<float>(), Z = new List<float>(), U = new List<float>(), V = new List<float>();
            List<int> mat = new List<int>();
            int curIdx = 0;
            mesh.Mats.Add(new Material());
            foreach (string raw in obj.Replace("\r", "").Split('\n'))
            {
                if (raw.Length < 2) continue;
                if (raw[0] == 'v' && raw[1] == ' ') { string[] p = raw.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); pos.Add(new float[] { P(p[1]), P(p[2]), P(p[3]) }); }
                else if (raw[0] == 'v' && raw[1] == 't') { string[] p = raw.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); uv.Add(new float[] { P(p[1]), P(p[2]) }); }
                else if (raw.StartsWith("usemtl "))
                {
                    string name = raw.Substring(7).Trim();
                    if (!matIndex.TryGetValue(name, out curIdx))
                    {
                        Material mm; if (!byName.TryGetValue(name, out mm)) mm = new Material();
                        mesh.Mats.Add(mm); curIdx = mesh.Mats.Count - 1; matIndex[name] = curIdx;
                    }
                }
                else if (raw[0] == 'f' && raw[1] == ' ')
                {
                    string[] p = raw.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int cnt = p.Length - 1;
                    int[] vi = new int[cnt], ti = new int[cnt];
                    for (int i = 0; i < cnt; i++)
                    {
                        string[] parts = p[i + 1].Split('/');
                        vi[i] = Idx(parts[0], pos.Count);
                        ti[i] = parts.Length > 1 && parts[1].Length > 0 ? Idx(parts[1], uv.Count) : -1;
                    }
                    for (int i = 1; i + 1 < cnt; i++)
                    {
                        int[] k = { 0, i, i + 1 };
                        foreach (int q in k)
                        {
                            float[] v = pos[vi[q]];
                            X.Add(v[0]); Y.Add(v[1]); Z.Add(v[2]);
                            float[] t = ti[q] >= 0 ? uv[ti[q]] : new float[] { 0, 0 };
                            U.Add(t[0]); V.Add(t[1]);
                        }
                        mat.Add(curIdx);
                    }
                }
            }
            mesh.X = X.ToArray(); mesh.Y = Y.ToArray(); mesh.Z = Z.ToArray(); mesh.U = U.ToArray(); mesh.V = V.ToArray(); mesh.Mat = mat.ToArray();
            return mesh;
        }

        static float P(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }
        static int Clamp(string s) { return Math.Max(0, Math.Min(255, (int)(P(s) * 255))); }
        static int Idx(string s, int count) { int i = int.Parse(s); return i > 0 ? i - 1 : count + i; }

        static void Fill(Material m, Bitmap b)
        {
            m.W = b.Width; m.H = b.Height; m.Pix = new int[m.W * m.H];
            BitmapData d = b.LockBits(new Rectangle(0, 0, m.W, m.H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(d.Scan0, m.Pix, 0, m.Pix.Length);
            b.UnlockBits(d);
        }
    }

    // draws the model with a small software rasteriser on a background thread
    public class AvatarView : IDisposable
    {
        public readonly int W = 480, H = 580;
        public volatile Mesh Model;
        public float Yaw, Pitch, Zoom = 1f;
        public bool Auto = true;
        volatile Bitmap frame;
        volatile bool stop;
        Thread thread;
        int[] pix; float[] zb;

        public AvatarView() { pix = new int[W * H]; zb = new float[W * H]; thread = new Thread(Loop) { IsBackground = true }; thread.Start(); }

        public Bitmap Frame { get { return frame; } }
        public void Dispose() { stop = true; }

        void Loop()
        {
            DateTime last = DateTime.UtcNow;
            while (!stop)
            {
                Mesh m = Model;
                if (m != null)
                {
                    DateTime now = DateTime.UtcNow;
                    if (Auto) Yaw += (float)(now - last).TotalSeconds * 0.6f;
                    last = now;
                    Render(m);
                    Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb);
                    BitmapData d = b.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    System.Runtime.InteropServices.Marshal.Copy(pix, 0, d.Scan0, pix.Length);
                    b.UnlockBits(d);
                    Bitmap old = frame; frame = b;
                    if (old != null) old.Dispose();
                }
                Thread.Sleep(24);
            }
            Bitmap f = frame; frame = null; if (f != null) f.Dispose();
        }

        void Render(Mesh m)
        {
            Array.Clear(pix, 0, pix.Length);
            for (int i = 0; i < zb.Length; i++) zb[i] = 0f;
            float[] f = Norm(m.Dir);
            float[] up = { 0, 1, 0 };
            float[] right = Norm(Cross(f, up));
            float[] up2 = Cross(right, f);
            float dx = m.Center[0] - m.Eye[0], dy = m.Center[1] - m.Eye[1], dz = m.Center[2] - m.Eye[2];
            float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dist < 0.01f) dist = 8f;
            dist /= Zoom;
            float[] eye = { m.Center[0] - f[0] * dist, m.Center[1] - f[1] * dist, m.Center[2] - f[2] * dist };
            float focal = (H / 2f) / (float)Math.Tan(m.Fov * Math.PI / 360.0);
            float cy = (float)Math.Cos(Yaw), sy = (float)Math.Sin(Yaw), cp = (float)Math.Cos(Pitch), sp = (float)Math.Sin(Pitch);
            int n = m.X.Length;
            float[] sx = new float[n], sY = new float[n], iz = new float[n], uz = new float[n], vz = new float[n], vx = new float[n], vy = new float[n], vzz = new float[n];
            for (int i = 0; i < n; i++)
            {
                float x = m.X[i] - m.Center[0], y = m.Y[i] - m.Center[1], z = m.Z[i] - m.Center[2];
                float x1 = x * cy + z * sy, z1 = -x * sy + z * cy;          // yaw
                float y2 = y * cp - z1 * sp, z2 = y * sp + z1 * cp;         // pitch
                x = x1 + m.Center[0] - eye[0]; y = y2 + m.Center[1] - eye[1]; z = z2 + m.Center[2] - eye[2];
                float cx = x * right[0] + y * right[1] + z * right[2];
                float cyy = x * up2[0] + y * up2[1] + z * up2[2];
                float cz = x * f[0] + y * f[1] + z * f[2];
                if (cz < 0.05f) cz = 0.05f;
                float inv = 1f / cz;
                vx[i] = cx; vy[i] = cyy; vzz[i] = cz;
                sx[i] = W / 2f + focal * cx * inv; sY[i] = H / 2f - focal * cyy * inv; iz[i] = inv;
                uz[i] = m.U[i] * inv; vz[i] = (1f - m.V[i]) * inv;
            }
            for (int t = 0; t < m.Mat.Length; t++)
            {
                int a = t * 3, b = a + 1, c = a + 2;
                float area = (sx[b] - sx[a]) * (sY[c] - sY[a]) - (sx[c] - sx[a]) * (sY[b] - sY[a]);
                if (Math.Abs(area) < 0.3f) continue;
                int minX = Math.Max(0, (int)Math.Floor(Math.Min(sx[a], Math.Min(sx[b], sx[c])))), maxX = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(sx[a], Math.Max(sx[b], sx[c]))));
                int minY = Math.Max(0, (int)Math.Floor(Math.Min(sY[a], Math.Min(sY[b], sY[c])))), maxY = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(sY[a], Math.Max(sY[b], sY[c]))));
                if (minX > maxX || minY > maxY) continue;
                float e1x = vx[b] - vx[a], e1y = vy[b] - vy[a], e1z = vzz[b] - vzz[a], e2x = vx[c] - vx[a], e2y = vy[c] - vy[a], e2z = vzz[c] - vzz[a];
                float nx = e1y * e2z - e1z * e2y, ny = e1z * e2x - e1x * e2z, nz = e1x * e2y - e1y * e2x;
                float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                float shade = nl < 1e-9f ? 1f : 0.5f + 0.5f * Math.Abs((nz * 0.8f + ny * 0.35f - nx * 0.25f) / nl);
                if (shade > 1f) shade = 1f;
                Material mat = m.Mats[m.Mat[t]];
                float inva = 1f / area;
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float w0 = ((sx[b] - px) * (sY[c] - py) - (sx[c] - px) * (sY[b] - py)) * inva;
                        float w1 = ((sx[c] - px) * (sY[a] - py) - (sx[a] - px) * (sY[c] - py)) * inva;
                        float w2 = 1f - w0 - w1;
                        if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                        float z = w0 * iz[a] + w1 * iz[b] + w2 * iz[c];
                        int idx = y * W + x;
                        if (z <= zb[idx]) continue;
                        int col;
                        if (mat.Pix != null)
                        {
                            float u = (w0 * uz[a] + w1 * uz[b] + w2 * uz[c]) / z, v = (w0 * vz[a] + w1 * vz[b] + w2 * vz[c]) / z;
                            int tx = (int)(u * mat.W) % mat.W, ty = (int)(v * mat.H) % mat.H;
                            if (tx < 0) tx += mat.W; if (ty < 0) ty += mat.H;
                            col = mat.Pix[ty * mat.W + tx];
                            if ((uint)col >> 24 < 128) continue;
                        }
                        else col = mat.Color;
                        zb[idx] = z;
                        int r = (int)(((col >> 16) & 255) * shade), g = (int)(((col >> 8) & 255) * shade), bl = (int)((col & 255) * shade);
                        pix[idx] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | bl;
                    }
            }
        }

        static float[] Cross(float[] a, float[] b) { return new float[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] }; }
        static float[] Norm(float[] v) { float l = (float)Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]); if (l < 1e-6f) return new float[] { 0, 0, -1 }; return new float[] { v[0] / l, v[1] / l, v[2] / l }; }
    }
}
