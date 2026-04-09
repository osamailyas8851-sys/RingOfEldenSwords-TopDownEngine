using UnityEngine;

namespace MoreMountains.TopDownEngine
{
    public enum PerkType
    {
        ExtraSword,
        SwordLevelUp,
        HealthUp
    }

    [CreateAssetMenu(fileName = "NewPerk", menuName = "TopDown Engine/Perks/Perk Definition")]
    public class PerkDefinition : ScriptableObject
    {
        public string PerkName;
        [TextArea] public string Description;
        public Sprite Icon;
        public PerkType PerkType;
        public float Value;
    }
}
