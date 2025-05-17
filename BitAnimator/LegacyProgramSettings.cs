
using UnityEngine;

namespace AudioVisualization
{
	public class LegacyProgramSettings : EngineSettings
	{
		public bool useNativeCore = true;
		public bool useAVX = true;
		public override int GetHashCode()
		{
			int hashCode = -130738350;
			hashCode = hashCode * -1521134295 + useNativeCore.GetHashCode();
			hashCode = hashCode * -1521134295 + useAVX.GetHashCode();
			return hashCode;
		}
		public override bool Equals(object other)
		{
			LegacyProgramSettings second = other as LegacyProgramSettings;
			if (ReferenceEquals(second, null))
				return false;
			return useNativeCore == second.useNativeCore && useAVX == second.useAVX;
		}
#if UNITY_EDITOR
		static GUIContent useNativeCoreLabel = new GUIContent("Use Native Core", "engine will use the native plugin BitAnimatorCore.dll for quick calculations");
		static GUIContent useAVXLabel = new GUIContent("Use AVX instructions", "allow the native plugin use AVX CPU instructions");
		public override void DrawProperty()
		{
			UnityEditor.EditorGUI.BeginChangeCheck();
			bool useNativeCore2 = UnityEditor.EditorGUILayout.Toggle(useNativeCoreLabel, useNativeCore);
			bool useAVX22 = UnityEditor.EditorGUILayout.Toggle(useAVXLabel, useAVX);
			if (UnityEditor.EditorGUI.EndChangeCheck())
			{
				UnityEditor.Undo.RecordObject(this, "LegacyProgram settings");
				useNativeCore = useNativeCore2;
				useAVX = useAVX22;
				UnityEditor.EditorUtility.SetDirty(this);
			}
		}
#endif
	}
}
