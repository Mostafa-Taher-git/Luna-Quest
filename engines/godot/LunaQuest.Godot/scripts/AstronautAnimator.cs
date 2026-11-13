using Godot;

namespace LunaQuest.Godot
{
    /// <summary>
    /// Drives the astronaut rig. The Sketchfab download contains one long action that
    /// starts as a seated bind pose, holds an idle, walks on the spot and finally drifts
    /// away. We reuse that data instead of shipping a second animation:
    ///   0.75s .. 1.75s  a gentle breathing idle
    ///   2.50s .. 5.00s  one complete walk cycle (the pose repeats exactly)
    /// Both sections are played as looping sections, so the suit walks in place while
    /// the player/NPC root moves through the world.
    /// </summary>
    public partial class AstronautAnimator : Node
    {
        private const string IdleAnimation = "LunaQuestIdle";
        private const string WalkAnimation = "LunaQuestWalk";

        // Sections of the imported action (seconds).
        private const float IdleStart = 0.75f;
        private const float IdleEnd = 1.75f;
        private const float WalkStart = 2.50f;
        private const float WalkEnd = 5.00f;

        private AnimationPlayer? _player;
        private bool _walking;
        private float _speedScale = 1f;

        public bool HasRig { get; private set; }

        public void Setup(AnimationPlayer player)
        {
            _player = player;

            string[] names = player.GetAnimationList();
            if (names.Length == 0) return;

            var source = player.GetAnimation(names[0]);
            if (source == null) return;

            AddLoopingCopy(player, source, IdleAnimation);
            AddLoopingCopy(player, source, WalkAnimation);

            HasRig = true;
            PlayIdle(0.0);
        }

        /// <summary>
        /// Switches between the idle and walk sections. Safe to call every frame: the
        /// animation only restarts when the movement state actually changes.
        /// </summary>
        public void SetLocomotion(bool moving, float speed)
        {
            if (!HasRig || _player == null) return;

            if (moving)
            {
                if (!_walking)
                {
                    _player.PlaySection(WalkAnimation, WalkStart, WalkEnd, 0.18);
                    _walking = true;
                }

                // A longer stride should not make the feet skate. Scale the cycle with
                // the ground speed, but keep it inside a readable range.
                float target = Mathf.Clamp(0.9f + speed * 0.16f, 1.15f, 2.2f);
                if (!Mathf.IsEqualApprox(_speedScale, target))
                {
                    _speedScale = target;
                    _player.SpeedScale = target;
                }
            }
            else if (_walking)
            {
                PlayIdle(0.25);
            }
        }

        private void PlayIdle(double blend)
        {
            if (!HasRig || _player == null) return;
            _player.PlaySection(IdleAnimation, IdleStart, IdleEnd, blend);
            _speedScale = 1f;
            _player.SpeedScale = 1f;
            _walking = false;
        }

        private static void AddLoopingCopy(AnimationPlayer player, Animation source, string name)
        {
            var library = player.GetAnimationLibrary("");
            if (library == null)
            {
                library = new AnimationLibrary();
                player.AddAnimationLibrary("", library);
            }

            if (library.HasAnimation(name))
                library.RemoveAnimation(name);

            var copy = (Animation)source.Duplicate();
            copy.LoopMode = Animation.LoopModeEnum.Linear;
            library.AddAnimation(name, copy);
        }
    }
}
