
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
	public class AmplitudeSmooth : Modificator
	{
		public float smoothness = 0.2f;
		public AmplitudeSmooth() { }
#if UNITY_EDITOR
		static GUIContent smoothnessLabel = new GUIContent("Smoothness", "Smooths the animation curve (value in seconds)");
		override public void DrawProperty()
		{
			EditorGUI.BeginChangeCheck();
			float smoothness2 = Mathf.Max(0.0f, EditorGUILayout.FloatField(smoothnessLabel, smoothness));
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(this, "Smoothness");
				smoothness = smoothness2;
				EditorUtility.SetDirty(this);
			}
		}
#endif
	}
}