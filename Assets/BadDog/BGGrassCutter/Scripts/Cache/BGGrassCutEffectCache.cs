using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BadDog
{
    public class BGGrassLeavePoolEffect
    {
        public GameObject effect;
        public float backTime;
        public int layer;
    }

    public class BGGrassCutEffectCache
    {
        private BGGrassCutManager m_GrassCutManager;

        private Dictionary<int, Queue<GameObject>> m_GrassCutEffectCache = new Dictionary<int, Queue<GameObject>>();
        private List<BGGrassLeavePoolEffect> m_LeavePoolEffectList = new List<BGGrassLeavePoolEffect>();

        private BGGrassLeavePoolEffect CreateLeavePoolEffect(GameObject effect, int layer, float life)
        {
            BGGrassLeavePoolEffect leaveEffect = new BGGrassLeavePoolEffect();

            leaveEffect.effect = effect;
            leaveEffect.backTime = Time.time + life;
            leaveEffect.layer = layer;

            return leaveEffect;
        }

        private void SetEffectParent(BGGrassCutManager grassCutManager, GameObject effect)
        {
            effect.transform.parent = grassCutManager.gameObject.transform;
            effect.transform.localPosition = Vector3.zero;
            effect.SetActive(false);
        }

        private void AddNewInstanceToPool(BGGrassCutManager grassCutManager, int layer, Queue<GameObject> effectQueue)
        {
            BGGrassCutEffectLayerInfo grassCutEffect = grassCutManager.grassCutEffects[layer];

            if (grassCutEffect != null && grassCutEffect.effectTemplate != null)
            {
                GameObject effect = GameObject.Instantiate(grassCutEffect.effectTemplate);

                SetEffectParent(grassCutManager, effect);

                effectQueue.Enqueue(effect);
            }
        }

        public void Init(BGGrassCutManager grassCutManager, int defaultCacheCount = 4)
        {
            Clear();

            m_GrassCutManager = grassCutManager;

            for(int layer = 0; layer < m_GrassCutManager.grassCutEffects.Length; layer++)
            {
                Queue<GameObject> effectQueue = new Queue<GameObject>();

                for(int i = 0; i < defaultCacheCount; i++)
                {
                    AddNewInstanceToPool(grassCutManager, layer, effectQueue);
                }

                m_GrassCutEffectCache.Add(layer, effectQueue);
            }
        }

        public void Clear()
        {
            foreach(KeyValuePair<int ,Queue<GameObject>> kvp in m_GrassCutEffectCache)
            {
                foreach(GameObject effect in kvp.Value)
                {
                    GameObject.Destroy(effect);
                }

                kvp.Value.Clear();
            }

            m_GrassCutEffectCache.Clear();

            foreach(BGGrassLeavePoolEffect leaveEffect in m_LeavePoolEffectList)
            {
                GameObject.Destroy(leaveEffect.effect);
            }

            m_LeavePoolEffectList.Clear();
        }

        public GameObject Get(int layer, float life = 3.0f)
        {
            Queue<GameObject> effectQueue;

            if(m_GrassCutEffectCache.TryGetValue(layer, out effectQueue))
            {
                if (effectQueue.Count <= 0)
                {
                    AddNewInstanceToPool(m_GrassCutManager, layer, effectQueue);
                }

                GameObject effect = effectQueue.Dequeue();

                if (effect != null)
                {
                    m_LeavePoolEffectList.Add(CreateLeavePoolEffect(effect, layer, life));
                }

                return effect;
            }

            return null;
        }

        public void Put(BGGrassLeavePoolEffect leaveEffect)
        {
            Queue<GameObject> effectQueue;

            if (m_GrassCutEffectCache.TryGetValue(leaveEffect.layer, out effectQueue))
            {
                effectQueue.Enqueue(leaveEffect.effect);

                SetEffectParent(m_GrassCutManager, leaveEffect.effect);
            }
        }

        public void UpdateLeavePoolEffects()
        {
            float currentTime = Time.time;

            for(int i = m_LeavePoolEffectList.Count - 1; i >=0; i--)
            {
                BGGrassLeavePoolEffect leaveEffect = m_LeavePoolEffectList[i];

                if(leaveEffect.backTime < currentTime)
                {
                    m_LeavePoolEffectList.RemoveAt(i);

                    Put(leaveEffect);
                }
            }
        }
    }
}
