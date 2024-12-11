using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using YY.Enemy;
using YY.MainGame;

public class MonoGameManager : MonoBehaviour {
    public static MonoGameManager Ins;
    public Action cb;
    private void Start() {
        Ins = this;
    }
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
    private void OnDrawGizmos() {
        cb?.Invoke();
    }
}
