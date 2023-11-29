using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BadDog
{
    public class SimpleCamera : MonoBehaviour
    {
        public Transform lookAt;

        public Vector3 offset = new Vector3(0f, 10.1f, -13.1f);
        public Vector3 rotation = new Vector3(35f, 0f, 0f);

        public float followSpeed = 10.0f;


        void LateUpdate()
        {
            if (lookAt == null)
            {
                return;
            }

            Vector3 targetPos = lookAt.position + offset;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

            transform.rotation = Quaternion.Euler(rotation);
        }
    }
}

