using System;
using System.Collections.Generic;
using Godot;
namespace LunaQuest.Godot
{
    /// <summary>
    /// Scripted camera director that films the real, running colony. It is used to record the
    /// intro film (<c>--cinematic</c> together with Godot's <c>--write-movie</c>), so the opening
    /// of the game is the game itself and not a mock-up. All time comes from the frame delta, so a
    /// fixed-fps movie render is perfectly repeatable.
    /// </summary>
    public partial class Cinematic : Node
    {
        public const double Duration = 72.0;
        private sealed class Shot
        {
            public double Start, End;
            public Vector3[] Pos = Array.Empty<Vector3>();
            public Vector3[] Look = Array.Empty<Vector3>();
            public float FovA = 55f, FovB = 55f;
        }
        private sealed class Overlay
        {
            public double T0, T1;
            public Control Node = null!;
        }
        private Main _main = null!;
        private Camera3D _camera = null!;
        private ColorRect _fade = null!;
        private readonly List<Shot> _shots = new List<Shot>();
        private readonly List<Overlay> _overlays = new List<Overlay>();
        private double _t;
        private bool _dusted;
        public bool Finished { get; private set; }
        public void Begin(Main main)
        {
            _main = main;
            ProcessMode = ProcessModeEnum.Always;
            _main.Hud.Visible = false;
            _main.Player.InputLocked = true;
            _main.Player.GlobalPosition = new Vector3(2.2f, 0.1f, 40f);
            Input.MouseMode = Input.MouseModeEnum.Visible;
            HideWorldLabels(_main.World);
            _camera = new Camera3D { Name = "CineCamera", Fov = 55f, Near = 0.1f, Far = 4000f, Current = true };
            _main.AddChild(_camera);
            DefineShots();
            BuildOverlays();
            foreach (var arg in OS.GetCmdlineArgs())
                if (arg.StartsWith("--cine-seek=", StringComparison.Ordinal) &&
                    double.TryParse(arg.Substring(12), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double seek))
                    _t = seek;
            if (_t > 49.0) { _dusted = true; _main.Sim.State.ArrayDust = 0.7; }
            Apply(_t);
        }
        private static void HideWorldLabels(Node n)
        {
            foreach (var c in n.GetChildren())
            {
                if (c is Label3D l) l.Visible = false;
                HideWorldLabels(c);
            }
        }
        // ---- storyboard ------------------------------------------------------
        private void DefineShots()
        {
            // A. Earth over the rim: the first image is the planet everyone left behind.
            _shots.Add(new Shot
            {
                Start = 0, End = 7.0, FovA = 46f, FovB = 52f,
                Pos = new[] { new Vector3(60, 1.8f, -95), new Vector3(54, 2.0f, -108), new Vector3(48, 2.2f, -122) },
                Look = new[] { new Vector3(95, 70, -305), new Vector3(90, 52, -305), new Vector3(80, 34, -305) },
            });
            // B. Backlit approach: the Sun sits on the horizon behind the colony, shadows reach for the lens.
            _shots.Add(new Shot
            {
                Start = 7.0, End = 15.0, FovA = 54f, FovB = 50f,
                Pos = new[] { new Vector3(-52, 1.4f, -108), new Vector3(-40, 1.6f, -96), new Vector3(-26, 1.9f, -84) },
                Look = new[] { new Vector3(0, 6, 12), new Vector3(0, 6.5f, 22), new Vector3(0, 7, 32) },
            });
            // C. Crane around the colony: the whole layout at once.
            _shots.Add(new Shot
            {
                Start = 15.0, End = 25.2, FovA = 60f, FovB = 56f,
                Pos = new[] { new Vector3(-95, 20, 88), new Vector3(-30, 24, 104), new Vector3(45, 20, 96), new Vector3(100, 15, 62) },
                Look = new[] { new Vector3(-10, 3, -6), new Vector3(0, 3, -6), new Vector3(10, 3, -6), new Vector3(20, 3, -6) },
            });
            // D. Life support: the air and water that the numbers are about.
            _shots.Add(new Shot
            {
                Start = 25.2, End = 33.2, FovA = 52f, FovB = 46f,
                Pos = new[] { new Vector3(62, 1.9f, 50), new Vector3(56, 2.2f, 44), new Vector3(50, 2.6f, 40) },
                Look = new[] { new Vector3(42, 2.4f, 26), new Vector3(40, 2.6f, 24), new Vector3(38, 3.0f, 20) },
            });
            // E. The power yard that keeps all of it running.
            _shots.Add(new Shot
            {
                Start = 33.2, End = 41.3, FovA = 52f, FovB = 46f,
                Pos = new[] { new Vector3(-82, 3.2f, 26), new Vector3(-80, 3.6f, 18), new Vector3(-78, 4.0f, 10) },
                Look = new[] { new Vector3(-56, 1.6f, 2), new Vector3(-56, 1.6f, 0), new Vector3(-56, 1.6f, -4) },
            });
            // F. The crew and the commander on the plaza, filmed from the landing pad.
            _shots.Add(new Shot
            {
                Start = 41.3, End = 50.3, FovA = 50f, FovB = 44f,
                Pos = new[] { new Vector3(-24, 1.8f, 27), new Vector3(-21, 1.9f, 24), new Vector3(-18, 2.0f, 21) },
                Look = new[] { new Vector3(0, 2.2f, 8), new Vector3(0, 2.4f, 8), new Vector3(0, 2.6f, 8) },
            });
            // G. Hazards: dust settling on the arrays.
            _shots.Add(new Shot
            {
                Start = 50.3, End = 62.0, FovA = 52f, FovB = 46f,
                Pos = new[] { new Vector3(-50, 9.0f, -44), new Vector3(-52, 7.5f, -38), new Vector3(-54, 6.0f, -32) },
                Look = new[] { new Vector3(-52, 1.0f, -2), new Vector3(-52, 1.0f, -2), new Vector3(-52, 1.0f, -2) },
            });
            // H. Title: pull back to the whole colony under the Earth.
            _shots.Add(new Shot
            {
                Start = 62.0, End = Duration + 0.1, FovA = 56f, FovB = 62f,
                Pos = new[] { new Vector3(4, 4.5f, 72), new Vector3(2, 8f, 104), new Vector3(0, 13f, 142) },
                Look = new[] { new Vector3(0, 12, -20), new Vector3(0, 14, -40), new Vector3(0, 16, -60) },
            });
        }

        // ---- overlays --------------------------------------------------------
        private void BuildOverlays()
        {
            var layer = new CanvasLayer { Layer = 60, Name = "CineOverlay" };
            AddChild(layer);
            var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(root);
            // Letterbox bars give it the proportions of a film, not a game window.
            foreach (bool top in new[] { true, false })
            {
                var bar = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
                bar.SetAnchorsPreset(top ? Control.LayoutPreset.TopWide : Control.LayoutPreset.BottomWide);
                bar.CustomMinimumSize = new Vector2(0, 62);
                bar.OffsetBottom = top ? 62 : 0;
                bar.OffsetTop = top ? 0 : -62;
                root.AddChild(bar);
            }
            var shade = new TextureRect { MouseFilter = Control.MouseFilterEnum.Ignore, StretchMode = TextureRect.StretchModeEnum.Scale };
            var grad = new Gradient();
            grad.SetColor(0, new Color(0, 0, 0, 0));
            grad.SetColor(1, new Color(0.01f, 0.02f, 0.05f, 0.78f));
            shade.Texture = new GradientTexture2D { Gradient = grad, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1), Width = 4, Height = 256 };
            shade.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            shade.OffsetTop = -330;
            shade.OffsetBottom = -62;
            root.AddChild(shade);
            Add(root, 1.2, 7.0, Caption("SHACKLETON RIM  ·  89.9° S", "Lunar south pole  ·  the Sun never rises more than 2° above the horizon"));
            Add(root, 7.6, 14.2, Caption("LOW SUN. LONG SHADOWS.", "No atmosphere  ·  no weather  ·  no second chances"));
            Add(root, 16.0, 24.4, Big("7 PEOPLE  ·  1 ROBOT", "ONE SHARED COLONY", false));
            Add(root, 26.0, 30.0, Stat("0.84 kg", "oxygen per person, per day"));
            Add(root, 30.4, 33.0, Stat("11 L", "water per person, per day"));
            Add(root, 33.6, 37.4, Stat("2,800 kcal", "food per person, per day"));
            Add(root, 37.8, 41.0, Stat("30 kW", "peak solar power  ·  the Sun sets the budget"));
            Add(root, 42.0, 50.0, Big("30 EARTH DAYS", "SIX CREW  ·  ONE COMMANDER", false));
            Add(root, 51.0, 54.3, Stat("DUST", "on the arrays: output falls, the battery pays"));
            Add(root, 54.7, 57.8, Stat("DRIFT", "in the sensors: the reading lies"));
            Add(root, 58.2, 61.6, Stat("STORM", "solar particles: shelter, or take the dose"));
            Add(root, 63.0, 71.2, Title());
        }
        private LabelSettings Settings(int size, Color color, int outline = 0)
        {
            var s = new LabelSettings
            {
                FontSize = size,
                FontColor = color,
                OutlineSize = outline,
                OutlineColor = new Color(0, 0, 0, 0.85f),
            };
            if (L.LatinFont != null) s.Font = L.LatinFont;
            return s;
        }
        private Label MakeLabel(string text, int size, Color color, int outline = 4)
            => new Label { Text = text, LabelSettings = Settings(size, color, outline), MouseFilter = Control.MouseFilterEnum.Ignore };
        private Control Caption(string title, string sub)
        {
            var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            box.Position = new Vector2(80, 520);
            box.AddChild(MakeLabel(title, 40, new Color(0.9f, 0.96f, 1f)));
            box.AddChild(MakeLabel(sub, 23, new Color(0.62f, 0.78f, 0.92f)));
            return box;
        }
        private Control Stat(string number, string caption)
        {
            var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            box.Position = new Vector2(80, 470);
            box.AddChild(MakeLabel(number, 84, new Color(0.55f, 0.9f, 1f), 6));
            box.AddChild(MakeLabel(caption, 27, new Color(0.92f, 0.96f, 1f)));
            return box;
        }
        private Control Big(string head, string sub, bool center)
        {
            var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            box.Position = new Vector2(80, 470);
            box.AddChild(MakeLabel(head, 76, new Color(0.96f, 0.98f, 1f), 6));
            box.AddChild(MakeLabel(sub, 26, new Color(0.62f, 0.82f, 0.95f)));
            return box;
        }
        private Control Title()
        {
            var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Begin };
            box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            box.OffsetTop = 120;
            var name = MakeLabel("LUNA-QUEST", 140, new Color(0.97f, 0.99f, 1f), 8);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            var sub = MakeLabel("THE LIVING MOON COLONY", 40, new Color(0.55f, 0.85f, 1f), 4);
            sub.HorizontalAlignment = HorizontalAlignment.Center;
            var line = MakeLabel("Real numbers. Real Moon. Thirty days.", 27, new Color(0.88f, 0.93f, 1f), 3);
            line.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(name);
            box.AddChild(sub);
            box.AddChild(line);
            return box;
        }
        private void Add(Control root, double t0, double t1, Control node)
        {
            node.Modulate = new Color(1, 1, 1, 0);
            root.AddChild(node);
            _overlays.Add(new Overlay { T0 = t0, T1 = t1, Node = node });
        }
        // ---- playback --------------------------------------------------------
        public override void _Process(double delta)
        {
            if (Finished) return;
            if (_fade == null)
            {
                var layer = new CanvasLayer { Layer = 70, Name = "CineFade" };
                AddChild(layer);
                _fade = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
                _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                layer.AddChild(_fade);
            }
            _t += delta;
            // The colony keeps living while it is filmed: crew walk their schedule.
            _main.Sim.Step(delta);
            if (!_dusted && _t > 49.0)
            {
                _dusted = true;
                _main.Sim.State.ArrayDust = 0.7;
            }
            Apply(_t);
            if (_t >= Duration)
            {
                Finished = true;
                GD.Print("Cinematic finished.");
                GetTree().Quit();
            }
        }
        private void Apply(double t)
        {
            Shot? shot = null;
            foreach (var s in _shots)
                if (t >= s.Start && t < s.End) { shot = s; break; }
            shot ??= _shots[_shots.Count - 1];
            double u = Mathf.Clamp((t - shot.Start) / (shot.End - shot.Start), 0.0, 1.0);
            // A gentle ease so every move starts and ends softly but never stops dead.
            double eased = Mathf.Lerp(u, u * u * (3.0 - 2.0 * u), 0.55);
            Vector3 pos = Spline(shot.Pos, (float)eased);
            Vector3 look = Spline(shot.Look, (float)eased);
            // Camera heights are above the local ground, so a move over rough terrain never clips.
            pos.Y += (float)_main.World.Terrain.Height(pos.X, pos.Z);
            _camera.GlobalPosition = pos;
            _camera.LookAt(look, Vector3.Up);
            _camera.Fov = Mathf.Lerp(shot.FovA, shot.FovB, (float)eased);
            // Fade: from black at the start, dips at every cut, to black at the end.
            double alpha = 0.0;
            if (t < 1.6) alpha = 1.0 - t / 1.6;
            if (t > Duration - 1.6) alpha = Math.Max(alpha, (t - (Duration - 1.6)) / 1.6);
            foreach (var s in _shots)
            {
                if (s.Start <= 0.01) continue;
                double d = Math.Abs(t - s.Start);
                if (d < 0.22) alpha = Math.Max(alpha, 1.0 - d / 0.22);
            }
            if (_fade != null) _fade.Color = new Color(0, 0, 0, (float)Mathf.Clamp(alpha, 0.0, 1.0));
            foreach (var o in _overlays)
            {
                double a = 0;
                if (t >= o.T0 && t <= o.T1)
                    a = Math.Min(Math.Min((t - o.T0) / 0.6, (o.T1 - t) / 0.6), 1.0);
                o.Node.Modulate = new Color(1, 1, 1, (float)Mathf.Clamp(a, 0.0, 1.0));
            }
        }
        private static Vector3 Spline(Vector3[] pts, float u)
        {
            if (pts.Length == 1) return pts[0];
            float f = Mathf.Clamp(u, 0f, 1f) * (pts.Length - 1);
            int i = Math.Min((int)f, pts.Length - 2);
            float x = f - i;
            Vector3 p0 = pts[Math.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Math.Min(i + 2, pts.Length - 1)];
            float x2 = x * x, x3 = x2 * x;
            return 0.5f * ((2f * p1) + (-p0 + p2) * x + (2f * p0 - 5f * p1 + 4f * p2 - p3) * x2 + (-p0 + 3f * p1 - 3f * p2 + p3) * x3);
        }
    }
}
