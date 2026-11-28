using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

public class Cinematica : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public PlayableDirector cine;
    public float duration = 10f;
    float t;

    public GameObject CineObj;
    public GameObject GamePlayObj;
    void Start()
    {
        duration = (float)cine.duration;
    }

    // Update is called once per frame
    void Update()
    {
        t += Time.deltaTime;

        if(t >= duration)
        {
            CineObj.SetActive(false);
            GamePlayObj.SetActive(true);
            Destroy(this);
        }
    }
}
