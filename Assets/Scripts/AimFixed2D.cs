using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AimFixed2D : MonoBehaviour
{
    [Tooltip("0=+X, 90=+Y, -90=-Y, 180=-X")]
    public float angleDeg = 0f;

    void OnValidate() => Apply();
    void Start() => Apply();
    void Apply() => transform.rotation = Quaternion.Euler(0, 0, angleDeg);
}