using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Settings : MonoBehaviour
{
    public Image backgroundImage;
    public Slider genVolSlider;
    public Slider sfxVolSlider;
    public Slider musVolSlider;
    private float genVol;    
    private float sfxVol;    
    private float musVol;    

    public bool toggleBackground = true;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void Awake()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        toggleBackground = sceneName.Equals("Ajustes");
    }

    public void ReturnToMenu()
    {
        SceneManager.LoadScene(0);
    }
    // Update is called once per frame
    void Update()
    {
        if (genVolSlider == null || sfxVolSlider == null || musVolSlider == null){
        return;
    }

    float newGen = genVolSlider.value;
        float newSfx = sfxVolSlider.value;
        float newMus = musVolSlider.value;

        // Solo actualiza y loguea cuando cambia algún valor
        if (newGen != genVol || newSfx != sfxVol || newMus != musVol)
        {
            genVol = newGen;
            sfxVol = newSfx;
            musVol = newMus;

            Debug.Log($"Settings:\nGeneral Volume: {genVol}\nEffects Volume: {sfxVol}\nMusic Volume: {musVol}");
        }
    }
}
