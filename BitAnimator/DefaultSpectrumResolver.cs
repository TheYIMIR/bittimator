
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AudioVisualization
{
	public class DefaultSpectrumResolver : SpectrumResolver, ISpectrumResolver
	{
		public int startFreq;
		public int endFreq;
#if UNITY_EDITOR
		static GUIContent frequencyLabel = new GUIContent("Frequencies (Hz)", "Frequency band-pass filter");
		static GUIContent frequencyStartLabel = new GUIContent("Start", "Frequency band-pass filter");
		static GUIContent frequencyEndLabel = new GUIContent("End", "Frequency band-pass filter");
		public override void DrawProperty()
		{
			int startFreq2 = startFreq;
			int endFreq2 = endFreq;
			using (var frequencyProperties = new EditorGUI.ChangeCheckScope())
			{
				if (BitAnimator.interfaceMode >= BitAnimator.InterfaceMode.Simple)
				{
					int band = 0;
					for (int b = 1; b < FrequencyBand.bandStart.Length; b++)
					{
						if (startFreq == FrequencyBand.bandStart[b] && endFreq == FrequencyBand.bandEnd[b])
						{
							band = b;
							break;
						}
					}
					EditorGUI.BeginChangeCheck();
					band = EditorGUILayout.Popup("Frequency preset", band, FrequencyBand.name);
					if (EditorGUI.EndChangeCheck() && band >= 1)
					{
						startFreq2 = FrequencyBand.bandStart[band];
						endFreq2 = FrequencyBand.bandEnd[band];
					}
				}
				if (BitAnimator.interfaceMode >= BitAnimator.InterfaceMode.Default)
				{
					int maxFrequency = bitAnimator.audioClip.frequency / 2;
					float visualStart = BitAnimator.FromLogFrequency(startFreq2 / (float)maxFrequency);
					float visualEnd = BitAnimator.FromLogFrequency(endFreq2 / (float)maxFrequency);

					EditorGUI.BeginChangeCheck();
					EditorGUILayout.MinMaxSlider(frequencyLabel, ref visualStart, ref visualEnd, 0.0f, 1.0f);
					if (EditorGUI.EndChangeCheck())
					{
						startFreq2 = Mathf.RoundToInt(maxFrequency * BitAnimator.ToLogFrequency(visualStart));
						endFreq2 = Mathf.RoundToInt(maxFrequency * BitAnimator.ToLogFrequency(visualEnd));
					}
					startFreq2 = Mathf.Clamp(EditorGUILayout.IntField(frequencyStartLabel, startFreq2), 0, maxFrequency);
					endFreq2 = Mathf.Clamp(EditorGUILayout.IntField(frequencyEndLabel, endFreq2), 0, maxFrequency * 2);
				}
				if(frequencyProperties.changed)
				{
					Undo.RecordObject(this, "Frequencies");
					startFreq = startFreq2;
					endFreq = endFreq2;
					EditorUtility.SetDirty(this);
				}
			}
		}
		public class FrequencyBand
		{
			public static string[] name = new string[] { "Custom", "Low-Bit", "Bit", "High-Bit", "OverHigh-Bit", "Mid", "Low-Highs", "Highs", "Over-Highs(16k)", "YouGotThat(20k)", "Peak" };
			public static int[] bandStart = new int[] { 0,  0,  50, 600, 1000, 2000, 2400, 4000,  16000, 16500, 20000 };
			public static int[] bandEnd   = new int[] { 0, 50, 150, 800, 2000, 2400, 4000, 16000, 16500, 20000, 22050 };
		}
#endif
	}
}
