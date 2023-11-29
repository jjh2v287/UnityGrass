using System;
using UnityEngine;

namespace BadDog
{
    [RequireComponent(typeof(CharacterController))]
    public class SimpleController : MonoBehaviour
    {
        public float moveSpeed = 3.0f;
        public float rotateSpeed = 6.0f;

        private CharacterController m_CharacterController;
        private Camera m_Camera;


        private void Start()
        {
            m_Camera = Camera.main;
            m_CharacterController = GetComponent<CharacterController>();
        }

        private void UpdateMove()
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            Vector3 moveDirtion = Vector3.zero;

            if (m_Camera != null)
            {
                Vector3 cameraForward = Vector3.Scale(m_Camera.transform.forward, new Vector3(1, 0, 1)).normalized;
                moveDirtion = v * cameraForward + h * m_Camera.transform.right;
            }
            else
            {
                moveDirtion = v * Vector3.forward + h * Vector3.right;
            }

            moveDirtion = moveDirtion.normalized;
            m_CharacterController.Move(moveDirtion * moveSpeed * Time.deltaTime);
        }

        private void UpdateRotation()
        {
            Ray ray = m_Camera.ScreenPointToRay(Input.mousePosition);

            RaycastHit rayHit;

            if (Physics.Raycast(ray, out rayHit, 1000))
            {
                Vector3 forwad = rayHit.point - transform.position;
                forwad.y = 0f;
                Quaternion rotation = Quaternion.LookRotation(forwad);
                transform.rotation = Quaternion.Lerp(transform.rotation, rotation, Time.deltaTime * rotateSpeed);
            }

        }

        private void LateUpdate()
        {
            UpdateMove();
            UpdateRotation();
        }
    }
}
