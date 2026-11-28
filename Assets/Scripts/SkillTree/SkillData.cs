using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSkill", menuName = "Skill Tree/Skill")]
public class SkillData : ScriptableObject
{
    [Header("Info")]
    public string displayName = "Skill";
    [TextArea(2, 4)] public string description;
    public Sprite icon;
    
    [Header("Effects")]
    public List<StatEffect> effects = new();

    [Header("Unlock")]
    [Tooltip("If checked, the player starts with this skill")]
    public bool unlockedAtStart;
    [Tooltip("ALL of these skills must be unlocked for this one to become available")]
    public List<SkillData> prerequisites = new();
}
