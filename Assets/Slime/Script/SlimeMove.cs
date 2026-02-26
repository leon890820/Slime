using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlimeMove : MonoBehaviour{

    public float speed = 5f;

    void Update() {
        float h = Input.GetAxisRaw("Horizontal"); 
        float v = Input.GetAxisRaw("Vertical");   

        Vector3 dir = new Vector3(h, 0f, v).normalized;
        transform.Translate(dir * speed * Time.deltaTime, Space.World);
    }
}
