﻿using System.Collections.Generic;
using UnityEngine;

public class GravitySource : MonoBehaviour
{
    void OnEnable () {
        CustomGravity.Register(this);
    }

    void OnDisable () {
        CustomGravity.Unregister(this);
    }
    
    public virtual Vector3 GetGravity (Vector3 position) {
        return Physics.gravity;
    }
}