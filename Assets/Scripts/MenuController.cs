using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using PixelCrushers.DialogueSystem;

public class MenuController : MonoBehaviour
{
    public string firstSceneName;

    public void StartNewGame()
    {
        SceneManager.LoadScene("Test_01", LoadSceneMode.Single);
    }
}
