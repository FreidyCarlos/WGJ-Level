using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightConeTestInput : MonoBehaviour

{
    public Transform player; // Referencia al jugador
    public float rotateSpeed = 180f;
    public bool followPlayer = true;

    void Update()
    {
        if (followPlayer && player != null)
        {
            // Obtiene dirección hacia el jugador
            Vector2 dir = (player.position - transform.position).normalized;

            if (dir.sqrMagnitude > 0.0001f)
                transform.right = dir; // el cono mira hacia el jugador (+X)
        }
        else
        {
            float z = Input.GetAxisRaw("Horizontal") * rotateSpeed * Time.deltaTime;
            transform.Rotate(0, 0, z);
        }
    }
}

/*{
    public bool aimWithMouse = true;
    public float rotateSpeed = 180f;

    void Update()
    {
        if (aimWithMouse)
        {
            Vector3 m = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            m.z = transform.position.z;
            Vector2 dir = (m - transform.position).normalized;
            if (dir.sqrMagnitude > 0.0001f)
                transform.right = dir; // el cono mira por +X
        }
        else
        {
            float z = Input.GetAxisRaw("Horizontal") * rotateSpeed * Time.deltaTime;
            transform.Rotate(0, 0, z);
        }
    }
}*/