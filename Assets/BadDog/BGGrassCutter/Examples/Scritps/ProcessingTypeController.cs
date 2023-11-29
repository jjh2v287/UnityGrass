using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BadDog
{
    public class ProcessingTypeController : MonoBehaviour
    {
        public Text statsTxt;
        private BGGrassCutter m_GrassCutter;

        void OnEnable()
        {
            m_GrassCutter = GetComponent<BGGrassCutter>();

            m_GrassCutter.processingType = BGGrassProcessingType.Manual;
            statsTxt.text = "Current: " + "Manual";
        }

        void Update()
        {
            if (Input.GetKeyUp(KeyCode.F1))
            {
                m_GrassCutter.processingType = BGGrassProcessingType.Manual;
                statsTxt.text = "Current: " + "Manual";
            }
            else if (Input.GetKeyUp(KeyCode.F2))
            {
                m_GrassCutter.processingType = BGGrassProcessingType.Update;
                statsTxt.text = "Current: " + "Update";
            }
            else if (Input.GetKeyUp(KeyCode.F3))
            {
                m_GrassCutter.processingType = BGGrassProcessingType.LateUpdate;
                statsTxt.text = "Current: " + "LateUpdate";
            }

            if (m_GrassCutter.processingType == BGGrassProcessingType.Manual)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    if (m_GrassCutter != null)
                    {
                        m_GrassCutter.Cut();
                    }
                }
            }
        }
    }
}
