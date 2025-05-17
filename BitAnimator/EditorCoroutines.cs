
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

//#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AudioVisualization
{
    public class EditorCoroutines : MonoBehaviour
    {
#if UNITY_EDITOR
		static List<IEnumerator> coroutines = new List<IEnumerator>();
        public static void Start(IEnumerator enumerator)
        {
            if (coroutines.Count == 0)
                EditorApplication.update += UpdateCoroutines;
            coroutines.Add(enumerator);
        }
        static void UpdateCoroutines()
        {
            for (int i = 0; i < coroutines.Count; i++)
            {
                CustomYieldInstruction instruction = coroutines[i].Current as CustomYieldInstruction;
                if (instruction != null && instruction.keepWaiting)
                    continue;

                if (!coroutines[i].MoveNext())
                {
                    coroutines.RemoveAt(i--);
                    if (coroutines.Count == 0)
                        EditorApplication.update -= UpdateCoroutines;
                }
            }
        }

        public static void StopAll()
        {
            if (coroutines.Count > 0)
            {
                EditorApplication.update -= UpdateCoroutines;
                coroutines.Clear();
            }
        }
#else
		static EditorCoroutines instance;
        public static void Start(IEnumerator enumerator)
        {
            if (instance == null)
				instance = new GameObject("Coroutines", typeof(EditorCoroutines)).GetComponent<EditorCoroutines>();
			instance.StartCoroutine(enumerator);
		}
        public static void StopAll()
        {
			instance.StopAllCoroutines();

		}
#endif
	}
}
//#endif