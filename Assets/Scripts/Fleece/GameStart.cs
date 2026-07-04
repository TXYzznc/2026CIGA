using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStart : MonoBehaviour
{
    public GameObject c0;
    public Transform c1_pr;
    private Vector3 c0_pos;
    private Quaternion c0_rot;
    public float speed = 10f;
    public bool open = false;

    void Start()
    {
        c0_pos = c0.transform.position;
        c0_rot = c0.transform.rotation;
    }

    void Update()
    {
        if (open)
        {
            c0.transform.position = Vector3.Lerp(c0.transform.position, c1_pr.position, Mathf.Min(speed * Time.deltaTime, 1f));
            c0.transform.rotation = Quaternion.Lerp(c0.transform.rotation, c1_pr.rotation, Mathf.Min(speed * Time.deltaTime, 1f));
        }
        else
        {
            c0.transform.position = Vector3.Lerp(c0.transform.position, c0_pos, Mathf.Min(speed * Time.deltaTime, 1f));
            c0.transform.rotation = Quaternion.Lerp(c0.transform.rotation, c0_rot, Mathf.Min(speed * Time.deltaTime, 1f));
        }
    }

    public void ChoiceGame()
    {
        open = !open;
    }

    public void Level1()
    {
        SceneManager.LoadScene("Level1");
    }

    public void Level2()
    {
        SceneManager.LoadScene("Level2");
    }

    public void Level3()
    {
        SceneManager.LoadScene("Level3");
    }
}