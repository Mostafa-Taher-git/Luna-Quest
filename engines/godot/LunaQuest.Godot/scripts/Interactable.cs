using Godot;

namespace LunaQuest.Godot
{
    /// <summary>
    /// A physical station in the world. Walking up and pressing E runs the matching action in
    /// the shared core. The prompt is deliberately short: the object should explain itself.
    /// </summary>
    public partial class Interactable : Area3D
    {
        public string BuildingId = "";
        public string InteractionId = "";
        public string PromptEn = "";
        public string PromptAr = "";
        public string Kind = "read";
        public string NpcId = "";
        public Label3D? Label;
        public MeshInstance3D? Screen;

        public string Prompt => L.T(PromptEn, PromptAr);

        public override void _Ready()
        {
            Monitoring = true;
            Monitorable = true;
            CollisionLayer = 0;
            CollisionMask = 2; // player body layer
            AddToGroup("interactable");

            var shape = new CollisionShape3D();
            shape.Shape = new SphereShape3D { Radius = 1.7f };
            AddChild(shape);
        }

        public void SetScreenText(string text)
        {
            if (Label != null) Label.Text = text;
        }
    }
}
