
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
	public class Remap : Modificator
	{
		public AnimationCurve remap;
		public Remap() { remap = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1, 3, 3)); }
#if UNITY_EDITOR
		SerializedObject serializedObject;
		SerializedProperty serializedRemap;
		override public void DrawProperty()
		{
			if (serializedObject == null)
			{
				serializedObject = new SerializedObject(this);
				serializedRemap = serializedObject.FindProperty("remap");
			}
			serializedObject.Update();

			EditorGUILayout.PropertyField(serializedRemap);

			serializedObject.ApplyModifiedProperties();
		}
#endif
	}
}