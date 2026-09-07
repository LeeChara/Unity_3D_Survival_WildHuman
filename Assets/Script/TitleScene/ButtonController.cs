using UnityEngine;

public class ButtonController : MonoBehaviour
{
    public void StartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
    }
    public void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Play 모드 종료
        #else
            Application.Quit(); // 빌드된 게임에서는 애플리케이션 자체를 종료
        #endif
    }
}
