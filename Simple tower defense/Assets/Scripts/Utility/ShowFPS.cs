using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowFPS : MonoBehaviour {
    public Text FPXTxt;

    private float m_LastUpdateShowTime = 0f;  //上一次更新帧率的时间;  
    private float m_UpdateShowDeltaTime = 0.5f;//更新帧率的时间间隔;  
    private int m_FrameUpdate = 0;//帧数;  
    private float m_FPS = 0;//帧率
    private void Start() {
        m_LastUpdateShowTime = Time.realtimeSinceStartup;
    }
    void Update() {
        m_FrameUpdate++;
        //FPS = 某段时间内的总帧数 / 某段时间
        if (Time.realtimeSinceStartup - m_LastUpdateShowTime >= m_UpdateShowDeltaTime) {
            m_FPS = m_FrameUpdate / (Time.realtimeSinceStartup - m_LastUpdateShowTime);
            m_FrameUpdate = 0;
            m_LastUpdateShowTime = Time.realtimeSinceStartup;
            float fps = 1.0f / Time.deltaTime * Time.timeScale;
            FPXTxt.text = $"FPS:{fps:F2}";
        }
    }
}
