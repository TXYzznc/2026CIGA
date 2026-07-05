using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStart : MonoBehaviour
{
    public GameObject animObj;

    public void ChoiceGame()
    {
        if (animObj == null)
        {
            SceneManager.LoadScene("Launch");
            return;
        }
        StartCoroutine(PlayAndLoad());
    }

    private IEnumerator PlayAndLoad()
    {
        animObj.SetActive(true);
        var anim = animObj.GetComponent<Animation>();
        if (anim != null)
        {
            anim.Stop();
            anim.Play();
            yield return new WaitForSeconds(anim.clip ? anim.clip.length : 1f);
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }
        SceneManager.LoadScene("Launch");
    }
}
