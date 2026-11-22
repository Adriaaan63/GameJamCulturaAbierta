using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SkillLineUI : MonoBehaviour
{
    [SerializeField] private SkillData from;
    [SerializeField] private SkillData to;

    public SkillData From => from;
    public SkillData To => to;

    public void SetColor(Color color) => GetComponent<Image>().color = color;
}
