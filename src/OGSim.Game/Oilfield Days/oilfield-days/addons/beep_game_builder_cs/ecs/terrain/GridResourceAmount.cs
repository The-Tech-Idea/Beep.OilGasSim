using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Resource id plus quantity, used by grid build costs and starting wallets.
    /// </summary>
    [GlobalClass]
    public partial class GridResourceAmount : Resource
    {
        [Export] public string ResourceId { get; set; } = "wood";
        [Export(PropertyHint.Range, "0,999999,1")] public int Amount { get; set; } = 1;
    }
}
