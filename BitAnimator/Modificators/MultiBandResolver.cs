
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

namespace AudioVisualization
{
	[Serializable]
	public class MultiBandResolver : SpectrumResolver, ISpectrumResolver
	{
		[Serializable]
		public struct Band
		{
			public float frequency;
			public float width;
			[Range(-1f, 1f)] public float value;
			public Band(float frequency = 100.0f, float width = 50.0f, float value = 1.0f)
			{
				this.frequency = frequency;
				this.width = width;
				this.value = value;
			}
		}
		public Band[] bands = new Band[1] { new Band(100, 50, 1) };
		public bool smoothBand = true;

#if UNITY_EDITOR
		static GUIContent plusLabel;
		static GUIContent minusLabel;
		static GUIContent smoothBandLabel = new GUIContent("Smooth band", "");

		//static GUIContent widthLabel = new GUIContent("width", "width of the frequency band");
		static GUIContent valueLabel = new GUIContent("value", "affect multiplier");
		static GUIContent frequencyWidthLabel = new GUIContent("Width (Hz)", "Frequency band-pass filter");

		SerializedObject serializedObject;
		SerializedProperty serializedBands;
		SerializedProperty serializedSmoothBand;
		bool fold;
		override public void DrawProperty()
		{
			if (serializedObject == null)
			{
				serializedObject = new SerializedObject(this);
				serializedBands = serializedObject.FindProperty("bands");
				serializedSmoothBand = serializedObject.FindProperty("smoothBand");

				plusLabel = EditorGUIUtility.IconContent("Toolbar Plus", "Add sample");
				minusLabel = EditorGUIUtility.IconContent("Toolbar Minus", "Remove");
			}
			serializedObject.Update();

			fold = EditorGUILayout.Foldout(fold, new GUIContent("Frequency Bands"), true);
			if (fold)
			{
				int maxFrequency = bitAnimator.audioClip.frequency / 2;
				using (new EditorGUI.IndentLevelScope(+1))
				{
					for (int i = 0; i < serializedBands.arraySize; i++)
					{
						SerializedProperty band = serializedBands.GetArrayElementAtIndex(i);
						using (new EditorGUILayout.HorizontalScope())
						{
							using (new EditorGUILayout.VerticalScope())
							{
								SerializedProperty frequency = band.FindPropertyRelative("frequency");
								SerializedProperty width = band.FindPropertyRelative("width");
								SerializedProperty value = band.FindPropertyRelative("value");

								float frequency2 = frequency.floatValue;
								float width2 = width.floatValue;
								using (var frequencyProperties = new EditorGUI.ChangeCheckScope())
								{
									float visualFrequency = BitAnimator.FromLogFrequency(frequency2 / maxFrequency);
									float visualWidth = BitAnimator.FromLogFrequency(width2 / (maxFrequency * 2));

									EditorGUI.BeginChangeCheck();
									GUIContent frequencyLabel = new GUIContent("frequency " + (i + 1), "Frequency band-pass filter");
									visualFrequency = EditorGUILayout.Slider(frequencyLabel, visualFrequency, 0.0f, 1.0f);
									visualWidth = EditorGUILayout.Slider(frequencyWidthLabel, visualWidth, 0.0f, 1.0f);
									if (EditorGUI.EndChangeCheck())
									{
										frequency2 = Mathf.RoundToInt(maxFrequency * BitAnimator.ToLogFrequency(visualFrequency));
										width2 = Mathf.RoundToInt(maxFrequency * 2 * BitAnimator.ToLogFrequency(visualWidth));
									}
									frequency2 = Mathf.Clamp(EditorGUILayout.FloatField(frequencyLabel, frequency2), 0, maxFrequency);
									width2 = Mathf.Clamp(EditorGUILayout.FloatField(frequencyWidthLabel, width2), 0, maxFrequency * 2);
									if (frequencyProperties.changed)
									{
										frequency.floatValue = frequency2;
										width.floatValue = width2;
									}
								}
								EditorGUILayout.PropertyField(value, valueLabel);
							}
							if (GUILayout.Button(minusLabel, EditorStyles.miniButton, GUILayout.MaxWidth(24), GUILayout.MaxHeight(16)))
								serializedBands.DeleteArrayElementAtIndex(i--);
						}

						EditorGUILayout.Separator();
					}
					using (new EditorGUILayout.HorizontalScope())
					{
						GUILayout.FlexibleSpace();
						if (GUILayout.Button(plusLabel, EditorStyles.miniButton, GUILayout.MaxWidth(24), GUILayout.MaxHeight(16)))

							++serializedBands.arraySize;
					}
				}
			}
			EditorGUILayout.PropertyField(serializedSmoothBand, smoothBandLabel);
			
			serializedObject.ApplyModifiedProperties();
		}
#endif
	}
}