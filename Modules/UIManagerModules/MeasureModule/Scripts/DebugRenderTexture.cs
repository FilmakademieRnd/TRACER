using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugRenderTexture : MonoBehaviour{
    
    private Texture2D debugTexture;
    
    public void SetTexture(Texture2D _tex){
        debugTexture = _tex;
    }

    
    //debug
    void OnGUI(){
        if (debugTexture)
            GUI.DrawTexture(new Rect(0, 0, debugTexture.width, debugTexture.height), debugTexture);
    }
}
