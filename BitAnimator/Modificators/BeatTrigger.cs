
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
	public class BeatTrigger : Modificator
	{
		public float threshold = 0.1f;
		public float width = 1.0f;
		public float offset = 0.0f;
		public float window = 0.5f;
		public float[] values = new float[0];
		public BeatTrigger() {}
#if UNITY_EDITOR
		public bool debugDeltaOut;
		public bool debugRMSOut;
		bool fold;
		override public void DrawProperty()
		{
			GUILayout.Label("Beat Trigger");
			++EditorGUI.indentLevel;
			threshold = EditorGUILayout.Slider(new GUIContent("Threshold", "Volume higher this value will be mark as a beat"), threshold, 0.0f, 1.0f);
			width = EditorGUILayout.Slider(new GUIContent("Width", "Width a beat\n0 - 1 keyframe\n1 - 1 beat time"), width, 0.0f, 1.0f);
			offset = EditorGUILayout.Slider(new GUIContent("Offset", "Offset of a beat scaled with the beat time"), offset, -0.5f, 0.5f);
			window = EditorGUILayout.Slider(new GUIContent("Denoise", "Noise compensation analize window in seconds"), window, 0.0f, 4.0f);
			fold = EditorGUILayout.Foldout(fold, new GUIContent("States", "This values is an interpolation between Min and Max values. Every beat the trigger go to next state"), true);
			if (fold)
			{
				++EditorGUI.indentLevel;
				for (int i = 0; i < values.Length; i++)
				{
					EditorGUILayout.BeginHorizontal();
					values[i] = EditorGUILayout.Slider("[" + (i + 1) + "] :", values[i], -1.0f, 1.0f);
					
					if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus", "Remove"), EditorStyles.miniButton, GUILayout.MaxWidth(24), GUILayout.MaxHeight(16)))
						ArrayUtility.RemoveAt(ref values, i--);

					EditorGUILayout.EndHorizontal();
				}
				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus", "Add state"), EditorStyles.miniButton, GUILayout.MaxWidth(24), GUILayout.MaxHeight(16)))
					ArrayUtility.Add(ref values, 0.0f);

				EditorGUILayout.EndHorizontal();
				--EditorGUI.indentLevel;
			}
			if(BitAnimator.debugMode)
			{
				debugDeltaOut = EditorGUILayout.Toggle("Debug Delta", debugDeltaOut);
				debugRMSOut = EditorGUILayout.Toggle("Debug RMS", debugRMSOut);
			}
			--EditorGUI.indentLevel;
		}
#endif
	}
}
