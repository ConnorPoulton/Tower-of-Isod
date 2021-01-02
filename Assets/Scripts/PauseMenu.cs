using PixelCrushers.DialogueSystem;
using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    private bool isPaused = false;
    public GameObject pauseMenu;
    private void Start()
    {
        pauseMenu.SetActive(false);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            isPaused = !isPaused;
        }

        if (isPaused)
        {
            Time.timeScale = 0;
            DialogueManager.Pause();
            pauseMenu.SetActive(true);
        }
        else
        {
            Time.timeScale = 1;
            DialogueManager.Unpause();
            pauseMenu.SetActive(false);
        }
    }

    public void EndProgram()
    {
        Application.Quit();
    }
}
