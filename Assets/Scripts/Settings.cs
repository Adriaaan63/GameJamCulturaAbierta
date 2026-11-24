using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class Settings : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public void ReturnToMenu()
    {
        SceneManager.LoadScene(0);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
