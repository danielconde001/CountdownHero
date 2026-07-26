using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Respawns the player when they enter a hazard volume, with scene reload as a fallback.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DeathZone : MonoBehaviour
{
    [SerializeField] private bool reloadSceneIfNoRespawn = true;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerRespawn playerRespawn = other.GetComponentInParent<PlayerRespawn>();
        if (playerRespawn != null)
        {
            playerRespawn.Respawn();
            return;
        }

        if (reloadSceneIfNoRespawn)
        {
            other.GetComponent<PlayerAnimator>().PlayAnimationTrigger("Player Death");
            Scene activeScene = SceneManager.GetActiveScene();
            string sceneToReload = string.IsNullOrEmpty(activeScene.path)
                ? activeScene.name
                : activeScene.path;
            FadeManager.ShowFade( () => SceneManager.LoadScene(sceneToReload) );
        }
    }
}
