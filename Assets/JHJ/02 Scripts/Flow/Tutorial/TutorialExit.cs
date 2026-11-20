using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialExit : MonoBehaviour
{
    public void ReturnToTitle()
    {
        NetworkFlowManager manager = NetworkFlowManager.Instance;
        if (manager == null)
        {
            // GuideScene을 바로 실행한 경우(에디터 테스트) 매니저가 없음
            Debug.LogWarning("[TutorialExit] NetworkFlowManager 없음, 아웃게임 씬을 직접 불러옴");
            SceneManager.LoadScene(SceneNames.OUT_GAME);
            return;
        }

        manager.RequestLeaveGame();
    }
}
