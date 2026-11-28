using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum SkillState
{
    Locked,
    Available,
    Unlocked
}

public class SkillTreeUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Image skillIconImage;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI skillStatusAndReqText;
    [SerializeField] private TextMeshProUGUI skillDescriptionText;

    [Header("Appearance")]
    [SerializeField] private Color unlockedColor = new(0.95f, 0.7f, 0.2f);
    [SerializeField] private Color availableColor = new(0.2f, 0.55f, 0.75f);
    [SerializeField] private Color lockedColor = new(0.14f, 0.15f, 0.18f);
    [SerializeField] private Color unlockedLineColor = new(0.95f, 0.7f, 0.2f);
    [SerializeField] private Color availableLineColor = new(0.45f, 0.75f, 0.95f, 0.9f);
    [SerializeField] private Color lockedLineColor = new(0.4f, 0.4f, 0.45f, 0.25f);

    [Header("Detail Text Colors")]
    [SerializeField] private Color unlockedTextColor = new(0.95f, 0.7f, 0.2f);
    [SerializeField] private Color availableTextColor = new(0.45f, 0.75f, 0.95f);
    [SerializeField] private Color lockedTextColor = new(0.53f, 0.53f, 0.53f);
    [SerializeField] private Color missingRequirementColor = new(0.88f, 0.35f, 0.35f);

    [Header("Controls")]
    [SerializeField] private bool startOpen;
    [SerializeField] private InputAction toggleAction = new(binding: "<Keyboard>/tab");

    private const string DefaultDetail = "Hover over a skill to see its description";

    private readonly HashSet<SkillData> unlocked = new();
    private readonly List<SkillNodeUI> nodes = new();
    private readonly List<SkillLineUI> lines = new();
    
    public static event System.Action<StatEffect> EffectApplied;

    private void Start()
    {
        if (panel == null)
        {
            Debug.LogWarning("SkillTreeUI: panel is not assigned", this);
            return;
        }

        foreach (var node in panel.GetComponentsInChildren<SkillNodeUI>(true))
        {
            if (node.Skill == null)
            {
                Debug.LogWarning($"SkillTreeUI: node '{node.name}' has no SkillData", node);
                continue;
            }
            nodes.Add(node);
            node.Init(this);
            if (node.Skill.unlockedAtStart)
            {
                //unlocked.Add(node.Skill);
                Unlock(node.Skill);
            }
        }

        foreach (var line in panel.GetComponentsInChildren<SkillLineUI>(true))
        {
            if (line.From != null && line.To != null) lines.Add(line);
            else Debug.LogWarning($"SkillTreeUI: line '{line.name}' is missing From or To", line);
        }

        Refresh();
        ShowDetails(null);
        panel.SetActive(startOpen);
        ApplyPause();
    }

    private void OnEnable()
    {
        toggleAction.performed += OnTogglePerformed;
        toggleAction.Enable();
    }

    private void OnDisable()
    {
        toggleAction.performed -= OnTogglePerformed;
        toggleAction.Disable();
    }

    private void ApplyPause()
    {
        Time.timeScale = panel.activeSelf ? 0f : 1f;
    }
    
    private void OnTogglePerformed(InputAction.CallbackContext _) => Toggle();

    public void Toggle()
    {
        if (panel == null) return;
        panel.SetActive(!panel.activeSelf);
        ApplyPause();
    }

    public SkillState GetState(SkillData skill)
    {
        if (unlocked.Contains(skill)) return SkillState.Unlocked;

        foreach (var prerequisite in skill.prerequisites)
        {
            if (prerequisite != null && !unlocked.Contains(prerequisite)) return SkillState.Locked;
        }
        return SkillState.Available;
    }

    public void TryUnlock(SkillData skill)
    {
        if (GetState(skill) != SkillState.Available)
        {
            ShowDetails(skill);
            return;
        }

        //unlocked.Add(skill);
        Unlock(skill);
        Refresh();
        ShowDetails(skill);
    }

    public void ShowDetails(SkillData skill)
    {
        if (skill == null)
        {
            if (skillNameText != null) skillNameText.text = string.Empty;
            if (skillStatusAndReqText != null) skillStatusAndReqText.text = string.Empty;
            if (skillDescriptionText != null) skillDescriptionText.text = DefaultDetail;
            if (skillIconImage != null) skillIconImage.enabled = false;
            return;
        }

        if (skillNameText != null)
        {
            skillNameText.text = skill.displayName;
        }

        if (skillIconImage != null)
        {
            bool hasIcon = skill.icon != null;
            skillIconImage.sprite = skill.icon;
            skillIconImage.enabled = hasIcon;
        }

        if (skillStatusAndReqText != null)
        {
            string status = GetState(skill) switch
            {
                SkillState.Unlocked => Colored("Desbloqueada", unlockedTextColor),
                SkillState.Available => Colored("Disponible", availableTextColor),
                _ => Colored("Bloqueada", lockedTextColor)
            };

            string reqText = RequirementsText(skill);
            skillStatusAndReqText.text = string.IsNullOrEmpty(reqText)
                ? status
                : $"{status}  •  {reqText}";
        }

        if (skillDescriptionText != null)
        {
            skillDescriptionText.text = skill.description;
        }
    }

    private string RequirementsText(SkillData skill)
    {
        var parts = new List<string>();
        foreach (var prerequisite in skill.prerequisites)
        {
            if (prerequisite == null) continue;
            Color color = unlocked.Contains(prerequisite) ? unlockedTextColor : missingRequirementColor;
            parts.Add(Colored(prerequisite.displayName, color));
        }
        return parts.Count == 0 ? "" : $"\n<size=80%>Requires: {string.Join(", ", parts)}</size>";
    }

    private static string Colored(string text, Color color) =>
        $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    private void Refresh()
    {
        foreach (var node in nodes)
        {
            SkillState state = GetState(node.Skill);
            node.SetState(state, StateColor(state, unlockedColor, availableColor, lockedColor));
        }

        foreach (var line in lines)
        {
            line.SetColor(StateColor(LineState(line), unlockedLineColor, availableLineColor, lockedLineColor));
        }
    }

    private SkillState LineState(SkillLineUI line)
    {
        if (!unlocked.Contains(line.From)) return SkillState.Locked;
        return unlocked.Contains(line.To) ? SkillState.Unlocked : SkillState.Available;
    }

    private static Color StateColor(SkillState state, Color unlockedC, Color availableC, Color lockedC) => state switch
    {
        SkillState.Unlocked => unlockedC,
        SkillState.Available => availableC,
        _ => lockedC
    };
    
    private void Unlock(SkillData skill)
    {
        if (!unlocked.Add(skill)) return;
        foreach (var effect in skill.effects) EffectApplied?.Invoke(effect);
    }
    
    public void Close()
    {
        Time.timeScale = 1f;
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }
    
    private void OnDestroy() => Time.timeScale = 1f;
}
