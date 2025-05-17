
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT

// Version: 0.3 (02.12.2018)
// Version: 0.4 (01.06.2019)
// Version: 0.5 (25.06.2019)
// Version: 1.0 (14.07.2019)
// Version: 1.1 (01.09.2019)
// Version: 1.2 (31.12.2019)
// Version: 1.3 (01.10.2021)

using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using AudioVisualization.Modificators;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AudioVisualization
{
	public class BitAnimator : MonoBehaviour
	{
		public enum InterfaceMode
		{
			Simple, Default, Expert
		}
		public enum PropertyType // Extension for ShaderUtil.ShaderPropertyType
		{
			Color, Vector, Float, Range, TexEnv, Vector3, Quaternion, Int
		}
		public static InterfaceMode interfaceMode;
		public static bool debugMode;

		[Tooltip("Root gameobject with Animator component")]
		public Animator animatorObject;
		[Tooltip("Gameobject with shader/particles")]
		public GameObject targetObject;
		public AudioClip audioClip;
		public Plan plan = new Plan(_windowLogSize: 12, _mode: Mode.EnergyCorrection | Mode.LogAmplitude);
		public Engine.CoreType engineType = Engine.CoreType.Auto;
		public Engine engine;
		public EngineSettings[] engineSettings;
		public string[] serializedEngineSettings;

		public int bpm = -1;
		public float beatOffset;
		public bool loop = true;
		[Range(-1, 1)]
		public float deconvolution;
		//public string selectedEngine = "AudioVisualization.LegacyProgram";
		public string animationAssetPath;
		public AnimationClip animationClip;
		
		[Serializable]
		public class RecordSlot
		{
			public enum PropertiesSet
			{
				Material, ParticleSystem, BlendShape, Transform, Custom
			}
			public PropertyType type;
			public PropertiesSet typeSet;
			public string customTypeFullName;
			public string name;
			public string[] property;
			public string description;
			public Texture icon;
			public int startFreq;
			public int endFreq;
			public Vector4 minValue, maxValue;
			public float rangeMin, rangeMax;
			[HideInInspector] public float multiply = 1.0f;
			public Gradient colors;
			public int channelMask;
			public int loops;
			public List<Modificator> modificators = new List<Modificator>();
			public SerializedModificator[] serializedModificators = null;
			public SpectrumResolver resolver;
			public GameObject targetObject;
			public RecordSlot() { }
			public RecordSlot(RecordSlot slot)
			{
				type = slot.type;
				typeSet = slot.typeSet;
				customTypeFullName = slot.customTypeFullName;
				name = slot.name;
				property = slot.property;
				description = slot.description;
				icon = slot.icon;
				startFreq = slot.startFreq;
				endFreq = slot.endFreq;
				minValue = slot.minValue;
				maxValue = slot.maxValue;
				rangeMin = slot.rangeMin;
				rangeMax = slot.rangeMax;
				multiply = slot.multiply;
				colors = slot.colors;
				channelMask = slot.channelMask;
				loops = slot.loops;
				modificators = slot.modificators;
				resolver = slot.resolver;
				targetObject = slot.targetObject;
			}
			public void DeserializeModificators()
			{
				if(serializedModificators != null)
					for(int i = 0; i < serializedModificators.Length; i++)
						modificators[i] = serializedModificators[i].Deserialize(modificators[i]);
			}
			public void SerializeModificators()
			{
				serializedModificators = modificators.Where(mod => mod != null).Select(mod => new SerializedModificator(mod)).ToArray();
			}
		}
		public List<RecordSlot> recordSlots = new List<RecordSlot>();
		[HideInInspector]
		public Shader shader;
		public string presetName;

		void OnDestroy()
		{
			Release();
		}
		[ContextMenu("Unload engine")]
		public void Release()
		{
			if (engine != null) 
				engine.Dispose();
			engine = null;
		}
		public Type GetCurveAnimationType(Task task)
		{
			RecordSlot slot = task.slot;
			switch (slot.typeSet)
			{
				case RecordSlot.PropertiesSet.Material:
					return (slot.targetObject != null ? slot.targetObject : targetObject).GetComponent<Renderer>().GetType();
				case RecordSlot.PropertiesSet.ParticleSystem:
					return typeof(ParticleSystem);
				case RecordSlot.PropertiesSet.BlendShape:
					return typeof(SkinnedMeshRenderer);
				case RecordSlot.PropertiesSet.Transform:
					return typeof(Transform);
				case RecordSlot.PropertiesSet.Custom:
					return Type.GetType(slot.customTypeFullName);
			}
			return null;
		}
		public static string CalculateTransformPath(Transform targetTransform, Transform rootTransform)
		{

			string returnName = targetTransform.name;
			Transform tempObj = targetTransform;

			// it is the root transform
			if (tempObj == rootTransform)
				return "";

			while (tempObj.parent != rootTransform)
			{
				returnName = tempObj.parent.name + "/" + returnName;
				tempObj = tempObj.parent;
			}

			return returnName;
		}
		EngineSettings CreateEngineSettings(Engine.CoreType coreType)
		{
			switch(coreType)
			{
				case Engine.CoreType.Auto: return ScriptableObject.CreateInstance<LegacyProgramSettings>();
				case Engine.CoreType.Legacy: return ScriptableObject.CreateInstance<LegacyProgramSettings>();
				case Engine.CoreType.ComputeShaders: return ScriptableObject.CreateInstance<ComputeProgramSettings>();
				default: return null;
			}
		}
		public void LoadEngineSettings(Engine.CoreType coreType)
		{
			int i = (int)coreType;
			if (engineSettings[i] == null)
			{
				engineSettings[i] = CreateEngineSettings(coreType);
				if(!String.IsNullOrEmpty(serializedEngineSettings[i]))
					JsonUtility.FromJsonOverwrite(serializedEngineSettings[i], engineSettings[i]);
			}
		}
		public void SaveEngineSettings(Engine.CoreType coreType)
		{
			serializedEngineSettings[(int)coreType] = JsonUtility.ToJson(engineSettings[(int)coreType]);
		}
		public Engine CreateEngine(Engine.CoreType type)
		{
			switch (type)
			{
				case Engine.CoreType.Auto: return new LegacyProgram();
				case Engine.CoreType.Legacy: return new LegacyProgram();
				case Engine.CoreType.ComputeShaders: return new ComputeProgram();
				default: return null;
			}
		}
		static Engine.CoreType[] coreTypes = new Engine.CoreType[] { Engine.CoreType.Auto, Engine.CoreType.Legacy, Engine.CoreType.ComputeShaders };
		public void LoadEngineSettings()
		{
			if (serializedEngineSettings == null)
			{
				serializedEngineSettings = new string[coreTypes.Length];
			}
			else if (serializedEngineSettings.Length != coreTypes.Length)
			{
				Array.Resize(ref serializedEngineSettings, coreTypes.Length);
			}

			if (engineSettings == null)
			{
				engineSettings = new EngineSettings[coreTypes.Length];
			}
			else if (engineSettings.Length != coreTypes.Length)
			{
				Array.Resize(ref engineSettings, coreTypes.Length);
			}

			foreach (Engine.CoreType coreType in coreTypes)
			{
				LoadEngineSettings(coreType);
			}
		}
		public void SaveEngineSettings()
		{
			foreach (Engine.CoreType coreType in coreTypes)
			{
				SaveEngineSettings(coreType);
			}
		}
		public void SelectEngine(Engine.CoreType type)
		{
			if (ReferenceEquals(engine, null))
			{
				engine = CreateEngine(type);
			}
			else if (type == Engine.CoreType.Auto)
			{
				return;
			}
			else if (engine.Type != type)
			{
				engine.Dispose();
				engine = CreateEngine(type);
			}
		}
		public void InitializeEngine()
		{
			SelectEngine(engineType);
			engine.Initialize(engineSettings[(int)engineType], plan, audioClip);
		}
		public IEnumerator ComputeAnimation(AnimationClip clip, Action<BitAnimator> finishCallback = null)
		{
			yield return null;
			float time = Time.realtimeSinceStartup;
#if UNITY_EDITOR
			AnimationClipSettings s = new AnimationClipSettings();
			s.loopTime = loop;
			AnimationUtility.SetAnimationClipSettings(clip, s);
#endif
			InitializeEngine();

			if (bpm < 0)
			{
				IEnumerator e = engine.ComputeBPM();
				while (e.MoveNext())
					yield return e.Current;
				bpm = engine.bpm;
				beatOffset = engine.beatOffset;
			}
			else
			{
				engine.bpm = bpm;
				engine.beatOffset = beatOffset;
			}
			if(debugMode)
				Debug.LogFormat("[BitAnimator] Setup time = {0:F3}", Time.realtimeSinceStartup - time);
			time = Time.realtimeSinceStartup;

			if (animatorObject == null)
				animatorObject = GetComponent<Animator>();
			if (targetObject == null)
				targetObject = gameObject;

			IEnumerator i = engine.ComputeAnimation(recordSlots);
			while (i.MoveNext())
				yield return i.Current;

			engine.Status = "Writing animation clip";

			string go_path = CalculateTransformPath(targetObject.transform, animatorObject.transform);

			//Получаем результат с GPU и записываем в анимацию
			foreach (Task task in engine.GetTasks())
			{
				Type componentType = GetCurveAnimationType(task);
				string[] animationProperties = task.slot.property;
				for (int c = 0; c < animationProperties.Length; c++)
				{
					yield return null;
					Keyframe[] keyframes = task.GetKeyframes(c);
					keyframes[keyframes.Length - 1].time = audioClip.length;
					keyframes[keyframes.Length - 1].value = task.slot.minValue[c];
					keyframes[keyframes.Length - 1].inTangent = 0;
					AnimationCurve curve = new AnimationCurve(keyframes);
					clip.SetCurve(go_path, componentType, animationProperties[c], curve);
				}
				task.Dispose();
			}
			if(debugMode)
				Debug.LogFormat("[BitAnimator] Writing animation time = {0:F3}", Time.realtimeSinceStartup - time);
			engine.Status = "Done";
			if (finishCallback != null)
				finishCallback(this);
		}
		public IEnumerator CreateAnimation(Action<BitAnimator> finishCallback = null)
		{
			if (animationClip == null)
				yield break;

			if (animatorObject == null)
				animatorObject = GetComponent<Animator>();
			if (targetObject == null)
				targetObject = gameObject;

			animationClip.frameRate = Mathf.Max(animationClip.frameRate, (float)audioClip.frequency / plan.WindowSize * plan.multisamples);

			IEnumerator i = ComputeAnimation(animationClip, null);
			while (i.MoveNext())
				yield return i.Current;
#if UNITY_EDITOR
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
#endif
			if (finishCallback != null)
				finishCallback(this);
		}
		public void ClearAnimation()
		{
			animationClip.ClearCurves();
			animationClip.frameRate = 1;
#if UNITY_EDITOR
			AssetDatabase.SaveAssets();
#endif
		}

		[ContextMenu("Enable debug mode")]
		void SwitchDebugMode()
		{
			debugMode = !debugMode;
		}
		public static float ToLogFrequency(float normalizedFrequency)
		{
			return normalizedFrequency * Mathf.Pow(2.0f, 6.9f * (normalizedFrequency - 1.0f));
		}

		

		public static float FromLogFrequency(float x)
		{
			return 0.209086f * LambertW(571.191f * x);
		}
		public static float LambertW(float x, float prec = 1e-6f)
		{
			float w = 0;
			//float wTimesExpW = 0;
			//float wPlusOneTimesExpW = 0;
			for (int i = 64; i != 0; --i)
			{
				float expW = Mathf.Exp(w);
				float wTimesExpW = w * expW;
				float wPlusOneTimesExpW = wTimesExpW + expW;
				w -= (wTimesExpW - x) / (wPlusOneTimesExpW - (w + 2.0f) * (wTimesExpW - x) / (2.0f * w + 2.0f));
				if (prec > Mathf.Abs((x - wTimesExpW) / wPlusOneTimesExpW))
					return w;
			}
			//if (prec <= Mathf.Abs((x - wTimesExpW) / wPlusOneTimesExpW))
			throw new ArithmeticException("W(x) не сходится достаточно быстро при x = " + x);
		}
		public class RuntimeBinding
		{
			public GameObject target;
			public PropertyType type;
			public int channelMask;
			public Vector2Int freqBand;
			public virtual void EvaluatePropertyValue(float value)
			{

			}
		}
		public class ParticleSystemBind : RuntimeBinding
		{
			public struct Field
			{
				public string path;
				public string property;
				public PropertyType type;
				public bool separateAxis;
				public int mainAxis;
			}
			public ParticleSystem particleSystem;
			public Field field;
		}
		public class MaterialBind : RuntimeBinding
		{
			public struct Field
			{
				public string property;
				public PropertyType type;
			}
			public Material material;
			public Field field;
		}
		public class ShapeBind : RuntimeBinding
		{
			public SkinnedMeshRenderer mesh;
			public string shape;
			public float min, max;
			int index;
			public ShapeBind(SkinnedMeshRenderer skinnedMesh, string _shape)
			{
				mesh = skinnedMesh;
				shape = _shape;
				index = mesh.sharedMesh.GetBlendShapeIndex(shape);
			}
			public override void EvaluatePropertyValue(float value)
			{
				mesh.SetBlendShapeWeight(index, value);
			}
		}
		public class TransformBind : RuntimeBinding
		{
			public enum Field
			{
				Position, Rotation, Scale
			}
			Vector3 min = Vector3.zero;
			Vector3 max = Vector3.one;
			public Field field;
			public override void EvaluatePropertyValue(float value)
			{
				switch (field)
				{
					case Field.Position:
						target.transform.localPosition = Vector3.LerpUnclamped(min, max, value); break;
					case Field.Rotation:
						target.transform.localRotation = Quaternion.Euler(Vector3.LerpUnclamped(min, max, value)); break;
					case Field.Scale:
						target.transform.localScale = Vector3.LerpUnclamped(min, max, value); break;
				}
			}
		}
		
	}
}

