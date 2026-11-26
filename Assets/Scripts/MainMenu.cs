 using UnityEngine;
using UnityEngine.SceneManagement;
public class MainMenu : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public void LoadGame()
    {
        SceneManager.LoadScene(1);
    }
    
    public void ExitGame()
    {
        Application.Quit();
        Debug.Log("adios");
    }

    public void OpenSettings()
    {
        SceneManager.LoadScene(2);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
