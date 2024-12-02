using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowFSP : MonoBehaviour {
    public Text FPXTxt;
    void Start() {

    }

    // Update is called once per frame
    void Update() {
        // 获取当前帧率
        float fps = 1.0f / Time.deltaTime * Time.timeScale;
        FPXTxt.text = $"FPS:{fps:F2}";
    }
}
