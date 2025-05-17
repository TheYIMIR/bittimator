
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using System;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AudioVisualization.Modificators
{
	[Serializable]
	public class VolumeOverTime : Modificator
	{
		public AnimationCurve curve;
		bool fold;
		public VolumeOverTime() { }
		void Awake()
		{
			if(curve == null)
				curve = AnimationCurve.EaseInOut(0, 1, 60, 1);
		}
		public override void Initialize(BitAnimator bitAnimator, BitAnimator.RecordSlot slot)
		{
			InitializeSingleInstance<VolumeOverTime>(bitAnimator, slot);
		}
#if UNITY_EDITOR
		SerializedObject serializedObject;
		SerializedProperty serializedCurve;
		GUIContent label = new GUIContent("Volume over time", "Horizontal axis - time in seconds\nVertical - volume multiplyer");
		override public void DrawProperty()
		{
			if (serializedObject == null)
			{
				serializedObject = new SerializedObject(this);
				serializedCurve = serializedObject.FindProperty("curve");
			}
			serializedObject.Update();

			EditorGUILayout.PropertyField(serializedCurve, label);

			serializedObject.ApplyModifiedProperties();
		}
#endif
	}
}


