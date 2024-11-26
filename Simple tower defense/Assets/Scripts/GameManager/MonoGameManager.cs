using UnityEngine;
using UnityEngine.SocialPlatforms;

public class MonoGameManager : MonoBehaviour {
    private float currentTimeScale = 1;
    void Update() {
        if (Input.GetKeyDown(KeyCode.C)) {
            if (currentTimeScale.CompareToBool(1)) {
                currentTimeScale = 3;
                Time.timeScale = currentTimeScale;
            } else {
                currentTimeScale = 1;
                Time.timeScale = currentTimeScale;
            }
        }
    }
}
