using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EventReceivers : MonoBehaviour
{
    public void OnConversationEnter()
    {
        DialogueLua.SetVariable("Current_Scene", SceneManager.GetActiveScene().name);
    }

    public void OnConversationEnd()
    {
        DialogueLua.SetVariable("Last_Scene", SceneManager.GetActiveScene().name);
        SceneManager.LoadScene(DialogueLua.GetVariable("Next_Scene").AsString, LoadSceneMode.Single);
        DialogueLua.SetVariable("Next_Scene", "none");
    }
}
