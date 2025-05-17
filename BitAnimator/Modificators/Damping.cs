
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AudioVisualization.Modificators
{
	[Serializable]
	public class Damping: Modificator
	{
		public float damping = 0.2f;
#if UNITY_EDITOR
		public override void DrawProperty()
		{
			EditorGUI.BeginChangeCheck();
			float damping2 = EditorGUILayout.Slider(new GUIContent("Damping peaks", "How long peaks fading to minimum"), damping, 0, 1);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(this, "Damping");
				damping = damping2;
				EditorUtility.SetDirty(this);
			}
			GUILayout.Label(String.Format("50% lasts after {0:F2} second", -0.693147180f * damping / (damping - 1.0f)));
			GUILayout.Label(String.Format("{0:F1}% lasts after 1 second", 100.0f * Mathf.Exp(1.0f - 1.0f / damping)));
		}
#endif
	}
}
