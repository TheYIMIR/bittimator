
namespace AudioVisualization
{
	public class ComputeProgramSettings : EngineSettings
	{
#if UNITY_EDITOR
		public override void DrawProperty()
		{
			UnityEditor.EditorGUI.BeginChangeCheck();
			if (UnityEditor.EditorGUI.EndChangeCheck())
			{
				UnityEditor.Undo.RecordObject(this, "ComputeProgram settings");
				UnityEditor.EditorUtility.SetDirty(this);
			}
		}
#endif
	}
}
