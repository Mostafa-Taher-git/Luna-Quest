using System;
using Godot;
namespace LunaQuest.Godot
{
    /// <summary>
    /// Full-screen player for the opening film. It is skippable at any moment (Space, Enter, Esc
    /// or a click), never traps the player if the file is missing, and turns the 3D view off
    /// while it runs so the video gets the whole GPU.
    /// </summary>
    public partial class IntroPlayer : CanvasLayer
    {
        public const string VideoPath = "res://assets/video/intro.ogv";
        public event Action? Finished;
        private VideoStreamPlayer? _video;
        private Label _hint = null!;
        private ColorRect _black = null!;
        private double _t;
        private bool _done;
        public static bool IsAvailable() => ResourceLoader.Exists(VideoPath);
        public override void _Ready()
        {
            Layer = 100;
            Name = "IntroPlayer";
            ProcessMode = ProcessModeEnum.Always;
            _black = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Stop };
            _black.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_black);
            if (!IsAvailable()) { CallDeferred(nameof(Finish)); return; }
            var stream = GD.Load<VideoStream>(VideoPath);
            _video = new VideoStreamPlayer
            {
                Stream = stream,
                Expand = true,
                Autoplay = false,
                VolumeDb = -2f,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _video.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _video.Finished += Finish;
            AddChild(_video);
            _hint = new Label
            {
                Text = L.T("SPACE  ·  CLICK  ·  ESC  to skip", "مسافة  ·  نقرة  ·  ESC  للتخطي"),
                HorizontalAlignment = HorizontalAlignment.Right,
                Modulate = new Color(1, 1, 1, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _hint.AddThemeFontSizeOverride("font_size", 15);
            _hint.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 1f));
            _hint.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
            _hint.AddThemeConstantOverride("outline_size", 5);
            _hint.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
            _hint.OffsetLeft = -420; _hint.OffsetTop = -44; _hint.OffsetRight = -22; _hint.OffsetBottom = -14;
            AddChild(_hint);
            GetViewport().Disable3D = true;
            _video.Play();
        }
        public override void _Process(double delta)
        {
            if (_done) return;
            _t += delta;
            // The hint appears after a moment and fades out again, so it never sits on the title card.
            double a = Math.Min(Math.Max((_t - 1.2) / 0.6, 0.0), 1.0) * (_t < 9.0 ? 1.0 : Math.Max(1.0 - (_t - 9.0) / 1.0, 0.0));
            _hint.Modulate = new Color(1, 1, 1, (float)a * 0.85f);
        }
        public override void _Input(InputEvent @event)
        {
            if (_done) return;
            bool skip = false;
            if (@event is InputEventKey k && k.Pressed && !k.Echo &&
                (k.Keycode == Key.Space || k.Keycode == Key.Enter || k.Keycode == Key.Escape || k.Keycode == Key.KpEnter))
                skip = true;
            if (@event is InputEventMouseButton mb && mb.Pressed) skip = true;
            if (skip)
            {
                GetViewport().SetInputAsHandled();
                Finish();
            }
        }
        private void Finish()
        {
            if (_done) return;
            _done = true;
            _video?.Stop();
            GetViewport().Disable3D = false;
            Finished?.Invoke();
        }
    }
}
