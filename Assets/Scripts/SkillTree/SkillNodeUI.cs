using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
   
// Equipex, esto es para manejar un nodo individual en el árbol de habilidades.
// Créenlo a mano en la escena (Image + Button + este componente) como hijo del panel del árbol de habilidades.
// Hay que asegurarse de que cada nodo tenga asignada su SkillData y referencias de UI correctamente (a mano en la escena).
[RequireComponent(typeof(Button))]
public class SkillNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private SkillData skill;

    [Header("UI References")]
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI label;

    public SkillData Skill => skill;

    private SkillTreeUI tree;

    public void Init(SkillTreeUI owner)
    {
        tree = owner;
        GetComponent<Button>().onClick.AddListener(() => tree.TryUnlock(skill));

        if (label != null) label.text = skill.displayName;
        if (icon != null)
        {
            icon.sprite = skill.icon;
            icon.gameObject.SetActive(skill.icon != null);
        }
    }

    public void SetState(SkillState state, Color backgroundColor)
    {
        // Locked skills stay visible but dimmed; clicking them only shows the missing prerequisites
        float alpha = state == SkillState.Locked ? 0.35f : 1f;
        if (background != null) background.color = backgroundColor;
        if (icon != null) icon.color = new Color(1f, 1f, 1f, alpha);
        if (label != null) label.color = new Color(1f, 1f, 1f, state == SkillState.Unlocked ? 1f : alpha * 0.85f + 0.1f);
    }

    public void OnPointerEnter(PointerEventData eventData) => tree?.ShowDetails(skill);
    public void OnPointerExit(PointerEventData eventData) => tree?.ShowDetails(null);
}
