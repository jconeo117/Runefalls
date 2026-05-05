using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string nombreEscena = "Game";

    public void StartGame()
    {
        SceneManager.LoadScene(nombreEscena);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}