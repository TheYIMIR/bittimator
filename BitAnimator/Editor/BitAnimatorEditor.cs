
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.09.2021)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using AudioVisualization;
using AudioVisualization.Modificators;

[CustomEditor(typeof(BitAnimator))]
public class BitAnimatorEditor : Editor
{
	internal class ResolverFactory
	{
		internal static Type[] types;
		internal static string[] names;
		static ResolverFactory()
		{
			var data = new[] 
			{
				new{ name = "Default", type = typeof(DefaultSpectrumResolver) },
				new{ name = "Multi-Band", type = typeof(MultiBandResolver) }
			};
			names = data.Select(d => d.name).ToArray();
			types = data.Select(d => d.type).ToArray();
		}
		internal static SpectrumResolver CreateByIndex(int index)
		{
			return (SpectrumResolver)ScriptableObject.CreateInstance(types[index]);
		}
	}
	public struct Preset
	{
		public string name;
		public int fftWindowSize;
		public int multisamples;
		public float multiply;
		public DSPLib.DSP.Window.Type window;
		public float windowParameter;
		public float smoothSpectrum;
		public float damping;
		public float deСonvolution;
		public float convolution;
		public float frequencyBandStart;
		public float frequencyBandEnd;
		public float curveQuality;
		public bool calcFons;
		public bool deltaLoudness;
		public bool energyCorrection;
	}
	protected SerializedProperty animatorObject;
	protected SerializedProperty targetObject;
	protected SerializedProperty audioClip;
	protected SerializedProperty animationClip;
	protected SerializedProperty plan;
	protected SerializedProperty bpm;
	protected SerializedProperty beatOffset;
	//protected SerializedProperty selectedEngine;
	protected SerializedProperty engineType;
	protected SerializedProperty animationAsset;
	protected SerializedProperty recordSlots;
	protected SerializedProperty loop;
	//SerializedProperty presetName;

	ReorderableList[] modificatorLists;
	BitAnimator bitAnimator;
	Animator animatorGO;
	GameObject targetGO;
	Renderer renderer;
	ParticleSystem particles;
	AudioClip audio;

	bool repaint;
	bool advancedSettingsToggle;
	bool updateList = true;
	int selectedSlotIndex;
	bool[] expanded = new bool[0];
	Rect addPropertyButtonRect;
	List<BitAnimator.RecordSlot> availableVariables = new List<BitAnimator.RecordSlot>();

	GenericMenu menu = new GenericMenu();
	GenericMenu modMenu = new GenericMenu();

	Shader editorShader;
	//static Preset[] presets;
	static bool updateAnimation;
	[SerializeField] TextAsset serializedPresets;
	[SerializeField] Localization labels;

	public static BitAnimator selectedBitAnimator;
	public static BitAnimator.RecordSlot selectedSlot;
	//protected static List<EngineDescriptor> keyframingEngines;
	protected static Type[] supportedModificators;
	protected static Type[] supportedResolvers;
	//protected static Type[] engines;
	static GUIStyle testStyle;

	static BitAnimatorEditor()
	{
		//engines = typeof(BitAnimator).Assembly.GetTypes().Where(t => t.GetCustomAttributes().Cast<EngineInfoAttribute>().FirstOrDefault(a => a != null) != default).ToArray();
		//engines = typeof(BitAnimator).Assembly.GetTypes().Where(t => Attribute.IsDefined(t, typeof(EngineInfoAttribute))).ToArray();
		//keyframingEngines = GetAllEngines(renderer: false);
		//supportedModificators = FindAllModificators().ToArray();
		//engines = new Type[] { typeof(LegacyProgram), typeof(ComputeProgram) };
		supportedModificators = new Type[]
		{
			typeof(Accumulate),
			typeof(AmplitudeSmooth),
			typeof(BeatFinder),
			typeof(BeatTrigger),
			typeof(CustomPeaks),
			typeof(Damping),
			typeof(Normalize),
			typeof(Randomizer),
			typeof(Remap),
			typeof(Shift),
			typeof(TimeLimit),
			typeof(VolumeOverTime),
		};
	}
	/*public static List<EngineDescriptor> GetAllEngines(bool renderer = false)
	{
		List<EngineDescriptor> descriptors = new List<EngineDescriptor>();
		foreach(Type type in engines)
		{
			EngineInfoAttribute info = type.GetCustomAttributes(false)[0] as EngineInfoAttribute;
			if(renderer == info.Renderer)
			{
				EngineDescriptor engineDescriptor;
				engineDescriptor.name = info.Name;
				engineDescriptor.typeDescriptor = type.FullName;
				descriptors.Add(engineDescriptor);
			}
		}
		return descriptors;
	}
	public static IEnumerable<Type> FindAllModificators()
	{
		return Assembly.GetAssembly(typeof(Modificator)).GetTypes().Where(t => t.BaseType == typeof(Modificator));
	}*/
	public BitAnimatorEditor()
	{
		/*if (serializedPresets != null)
			presets = JsonUtility.FromJson<Preset[]>(serializedPresets.text);
		else
			presets = new Preset[0];*/
	}
	protected void OnEnable()
	{
		// Setup the SerializedProperties.
		BitAnimator.interfaceMode = (BitAnimator.InterfaceMode)EditorPrefs.GetInt("BitAnimator.InterfaceMode", (int)BitAnimator.InterfaceMode.Default);
		animatorObject = serializedObject.FindProperty ("animatorObject");
		targetObject = serializedObject.FindProperty ("targetObject");
		audioClip = serializedObject.FindProperty ("audioClip");
		animationClip = serializedObject.FindProperty("animationClip");
		plan = serializedObject.FindProperty ("plan");
		bpm = serializedObject.FindProperty("bpm");
		beatOffset = serializedObject.FindProperty("beatOffset");
		//selectedEngine = serializedObject.FindProperty("selectedEngine");
		engineType = serializedObject.FindProperty("engineType");
		animationAsset = serializedObject.FindProperty ("animationAssetPath");
		recordSlots = serializedObject.FindProperty ("recordSlots");
		loop = serializedObject.FindProperty ("loop");
		//presetName = serializedObject.FindProperty("presetName");

		bitAnimator = target as BitAnimator;

		bitAnimator.LoadEngineSettings();

		foreach (BitAnimator.RecordSlot slot in bitAnimator.recordSlots)
		{
			if (slot.icon == null)
			{
				switch(slot.typeSet)
				{
					case BitAnimator.RecordSlot.PropertiesSet.Material:
						slot.icon = EditorGUIUtility.ObjectContent(null, typeof(Material)).image;
						break;
					case BitAnimator.RecordSlot.PropertiesSet.ParticleSystem:
						slot.icon = EditorGUIUtility.ObjectContent(null, typeof(ParticleSystem)).image;
						break;
					case BitAnimator.RecordSlot.PropertiesSet.BlendShape:
						slot.icon = EditorGUIUtility.ObjectContent(null, typeof(SkinnedMeshRenderer)).image;
						break;
				}
			}
			for (int i = slot.modificators.Count - 1; i >= 0; i--)
			{
				if (slot.modificators[i] == null)
				{
					Debug.LogError("[BitAnimator] Null refferenced modificator", bitAnimator);
					slot.modificators.RemoveAt(i);
					EditorUtility.SetDirty(bitAnimator);
				}
			}
			if (slot.resolver == null)
			{
				Debug.LogError("[BitAnimator] Null refferenced resolver", bitAnimator);
				DefaultSpectrumResolver spectrumResolver = ScriptableObject.CreateInstance<DefaultSpectrumResolver>();
				spectrumResolver.startFreq = 0;
				spectrumResolver.endFreq = 150;
				slot.resolver = spectrumResolver;
				EditorUtility.SetDirty(bitAnimator);
			}
			slot.resolver.bitAnimator = bitAnimator;

			slot.DeserializeModificators();
		}

		EditorSceneManager.sceneSaving += SceneSavingCallback;
		RegisterMod(supportedModificators);

		InitReorderableLists();

		if (testStyle == null)
		{
			testStyle = new GUIStyle();
			testStyle.margin = new RectOffset(4, 4, 1, 0);
			testStyle.border = new RectOffset(2, 2, 8, 2);
			testStyle.padding = new RectOffset(4, 4, 1, 1);
		}
		recalcBpmLabel = EditorGUIUtility.IconContent("Refresh", "Recalculate BPM");
	}
	void SceneSavingCallback(Scene scene, string path)
	{
		foreach(BitAnimator.RecordSlot slot in bitAnimator.recordSlots)
			slot.SerializeModificators();
		bitAnimator.SaveEngineSettings();
	}
	protected void OnDestroy()
	{
		foreach(BitAnimator.RecordSlot slot in bitAnimator.recordSlots)
			slot.SerializeModificators();
		bitAnimator.SaveEngineSettings();
		EditorSceneManager.sceneSaving -= SceneSavingCallback;
	}
	
	void InitReorderableLists()
	{
		modificatorLists = new ReorderableList[recordSlots.arraySize];
		for (int i = 0; i < modificatorLists.Length; i++)
		{
			int slot = i;
			ReorderableList rList = modificatorLists[i] = new ReorderableList(serializedObject, recordSlots.GetArrayElementAtIndex(i).FindPropertyRelative("modificators"), true, true, true, true);
			rList.onAddDropdownCallback = (Rect buttonRect, ReorderableList list) =>
			{
				selectedSlot = bitAnimator.recordSlots[slot];
				modMenu.ShowAsContext();
			};
			rList.onRemoveCallback = (ReorderableList list) =>
			{
				Undo.RecordObject(bitAnimator, "Remove " + bitAnimator.recordSlots[slot].modificators[list.index].name);
				bitAnimator.recordSlots[slot].modificators.RemoveAt(list.index);
				EditorUtility.SetDirty(bitAnimator);
			};
			rList.drawHeaderCallback = (Rect rect) =>
			{
				EditorGUI.LabelField(rect, "Modificators", EditorStyles.boldLabel);
			};
			rList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
			{
				SerializedProperty target = rList.serializedProperty.GetArrayElementAtIndex(index);
				Modificator mod = (Modificator)target.objectReferenceValue;
				if (mod != null)
				{
					mod.enabled = GUI.Toggle(rect, mod.enabled, mod.name);
					if (isActive)
					{
						using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
						{
							mod.DrawProperty();
						}
					}
				}
			};
			rList.onReorderCallback = (ReorderableList list) =>
			{
				updateAnimation = true;
				if (BitAnimatorWindow.instance != null)
				{
					serializedObject.ApplyModifiedProperties();
					BitAnimatorWindow.instance.ResetView();
				}
			};
		}
	}
	void RegisterMod(IEnumerable<Type> modificators)
	{
		foreach (Type modType in modificators)
		{
			modMenu.AddItem(new GUIContent(modType.Name), false, () =>
			{
				Modificator mod = (Modificator)ScriptableObject.CreateInstance(modType);
				mod.Initialize(bitAnimator, selectedSlot);
				string typeName = modType.Name;
				string newModName = typeName;
				int number = 1;
				while(selectedSlot.modificators.Select(m => m.name).Contains(newModName))
					newModName = String.Concat(typeName, " (", (++number).ToString(), ")");

				mod.name = newModName;
				Undo.RecordObject(bitAnimator, "Add " + mod.name);
				selectedSlot.modificators.Add(mod);
				EditorUtility.SetDirty(bitAnimator);
			});
		}
	}
	static GUIContent audioSourceLabel = new GUIContent("Source Audioclip", "");
	static GUIContent animatorLabel = new GUIContent("Animator", "");
	static GUIContent targetGOLabel = new GUIContent("Target GameObject", "");
	static GUIContent animationClipLabel = new GUIContent("Animation clip", "Animation will be recorded to this animation clip");
	static GUIContent saveAnimationClipLabel = new GUIContent("...", "Save as...");
	static GUIContent testInPlayModeLabel = new GUIContent("Test in PlayMode", "Launch playmode, create an animation and test");
	static GUIContent testInEditModeLabel = new GUIContent("Test in EditMode", "Create an animation and test it now");
	static GUIContent updateAnimationLabel = new GUIContent("*Update animation*");
	static GUIContent recordAnimationLabel = new GUIContent("Record animation");
	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		using (var mainProperties = new EditorGUI.ChangeCheckScope())
		{
			using (var sourceAudioProperty = new EditorGUI.ChangeCheckScope())
			{
				EditorGUILayout.PropertyField(audioClip, audioSourceLabel);
				audio = audioClip.objectReferenceValue as AudioClip;
				if (sourceAudioProperty.changed)
					bpm.intValue = -1;
			}
			using (var rootAnimatorProperty = new EditorGUI.ChangeCheckScope())
			{
				EditorGUILayout.PropertyField(animatorObject, animatorLabel);
				animatorGO = animatorObject.objectReferenceValue as Animator;
				if (animatorGO == null)
				{
					animatorGO = bitAnimator.GetComponent<Animator>();
					if (animatorGO != null)
					{
						animatorObject.objectReferenceValue = animatorGO;
						GUI.changed = true;
					}
					else
					{
						serializedObject.ApplyModifiedProperties();
						return;
					}
				}
				if (rootAnimatorProperty.changed && audio == null && animatorGO != null)
				{
					AudioSource audioSource = animatorGO.GetComponentInChildren<AudioSource>();
					audio = audioSource != null ? audioSource.clip : null;
					audioClip.objectReferenceValue = audio;
				}
			}
			EditorGUILayout.PropertyField(targetObject, targetGOLabel);
			targetGO = targetObject.objectReferenceValue as GameObject;
			if (targetGO == null)
			{
				targetObject.objectReferenceValue = targetGO = animatorGO.gameObject;
				GUI.changed = true;
			}
			else if (targetGO != animatorGO && !targetGO.transform.IsChildOf(animatorGO.transform))
			{
				EditorGUILayout.HelpBox("Target gameobject must be a child of animator object", MessageType.Warning);
				serializedObject.ApplyModifiedProperties();
				return;
			}
			{
				Renderer currentRenderer = targetGO.GetComponent<Renderer>();
				if (currentRenderer != renderer)
					updateList = true;
				else if (currentRenderer != null && renderer != null)
				{
					Material currentMaterial = currentRenderer.sharedMaterial;
					Material material = renderer.sharedMaterial;
					if (currentMaterial != material)
						updateList = true;
					else if (currentMaterial != null && material != null)
					{
						Shader currentShader = currentMaterial.shader;
						if (editorShader != currentShader)
						{
							editorShader = currentShader;
							updateList = true;
						}
					}
				}
				ParticleSystem currentParticles = targetGO.GetComponent<ParticleSystem>();
				if (currentParticles != particles)
					updateList = true;
				renderer = currentRenderer;
				particles = currentParticles;
			}

			if (mainProperties.changed)
			{
				updateAnimation = true;
			}
		}
		if(audio == null)
		{
			serializedObject.ApplyModifiedProperties();
			return;
		}
		RenderAdvancedSettings();
		if(updateList)
		{
			menu = new GenericMenu();
			availableVariables.Clear();
			UpdateBlendShapeProperties();
			UpdateParticleProperties();
			UpdateShaderProperties();
			UpdateTransformProperties();
			UpdateCustomProperties();

			updateList = false;
		}

		RenderShaderProperties();
		RenderAddShaderProperties();
		using (new EditorGUILayout.HorizontalScope())
		{
			EditorGUILayout.PropertyField(animationClip, animationClipLabel);
			if (GUILayout.Button(saveAnimationClipLabel, GUILayout.MaxWidth(24), GUILayout.MaxHeight(16)))
			{
				animationAsset.stringValue = EditorUtility.SaveFilePanelInProject("BitAnimator", targetGO.name + ".anim", "anim", "Save animation clip");
				LoadOrCreateAnimationFile();
			}
		}
		EditorGUILayout.PropertyField(loop);

		serializedObject.ApplyModifiedProperties();

		EditorGUILayout.Space();

		bool unsupportedEngine = bitAnimator.engineType == Engine.CoreType.ComputeShaders;
		if(unsupportedEngine)
		{
			EditorGUILayout.HelpBox("ComputeShaders Engine is not supported by this BitAnimator version", MessageType.Error);
		}
		using (new EditorGUI.DisabledScope(!IsLoadableAudio() || BitAnimatorWindow.isRunningTask || unsupportedEngine))
		{
			Color oldBackground = GUI.backgroundColor;
			using (new EditorGUILayout.HorizontalScope())
			{
				if (EditorApplication.isPlaying)
				{
					GUI.backgroundColor = Color.Lerp(GUI.backgroundColor, 0.75f * Color.green, 0.5f);
					if (GUILayout.Button(updateAnimation && BitAnimatorWindow.animation != null ? "*Update animation*" : "Test animation", GUILayout.MinHeight(30)))
					{
						EditorCoroutines.Start(CreateAnimation());
					}
				}
				else
				{
					GUI.backgroundColor = Color.Lerp(GUI.backgroundColor, 0.75f * Color.green, 0.5f);
					if (GUILayout.Button(testInPlayModeLabel, GUILayout.MinHeight(30)))
					{
						BitAnimatorWindow window = BitAnimatorWindow.GetOrAddWindow();
						window.targetID = bitAnimator.GetInstanceID();
						EditorApplication.isPlaying = true;
					}
					GUI.backgroundColor = oldBackground;
					GUI.backgroundColor = Color.Lerp(GUI.backgroundColor, Color.blue + 0.5f * Color.green, 0.3f);

					GUIContent testInEditMode;
					if (updateAnimation && BitAnimatorWindow.animation != null)
						testInEditMode = updateAnimationLabel;
					else
						testInEditMode = testInEditModeLabel;

					if (GUILayout.Button(testInEditMode, GUILayout.MinHeight(30)))
					{
						EditorCoroutines.Start(CreateAnimation());
					}
				}
				GUI.backgroundColor = oldBackground;
				if (GUILayout.Button(recordAnimationLabel, GUILayout.MinHeight(30)))
				{
					if (animationClip.objectReferenceValue == null)
					{
						animationAsset.stringValue = EditorUtility.SaveFilePanelInProject("Save as...", targetGO.name + ".anim", "anim", "Save animation clip");
						LoadOrCreateAnimationFile();
						serializedObject.ApplyModifiedProperties();
					}
					if (animationClip.objectReferenceValue != null)
					{
						EditorCoroutines.Start(WriteAnimation());
					}
				}
			}
			GUILayout.Space(8);
			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();

				GUI.backgroundColor = Color.Lerp(GUI.backgroundColor, Color.red, 0.25f);
				if (!EditorApplication.isPlaying && BitAnimatorWindow.animation != null && BitAnimatorWindow.target == bitAnimator)
				{
					if (GUILayout.Button("Stop test", GUILayout.MaxWidth(80)))
						BitAnimatorWindow.ResetState();
				}
				GUI.backgroundColor = oldBackground;
			}

			if (BitAnimatorWindow.executionTime > 0 && !BitAnimatorWindow.isRunningTask)
			{
				EditorGUILayout.LabelField(string.Format("Recording time: {0:F3} sec.", BitAnimatorWindow.executionTime));
			}
		}
		if(repaint && BitAnimatorWindow.target == bitAnimator && bitAnimator.engine != null)
		{
			if (Event.current.type == EventType.Repaint)
			{
				Rect rect = GUILayoutUtility.GetRect(2048, 24);
				EditorGUI.ProgressBar(rect, bitAnimator.engine.Progress, bitAnimator.engine.Status);
			}
			else
			{ 
				GUILayout.Box(String.Empty, GUILayout.MinWidth(48), GUILayout.MinHeight(24), GUILayout.MaxWidth(2048), GUILayout.MaxHeight(24)); 
			}

			if(GUILayout.Button("Cancel", GUILayout.MaxWidth(80)))
			{
				BitAnimatorWindow.ResetState();
			}
		}
		repaint = BitAnimatorWindow.isRunningTask;
	}
	protected bool IsLoadableAudio()
	{
		bool isLoadableAudio = FFMPEG.IsInstalled || audio.loadType == AudioClipLoadType.DecompressOnLoad;
		if (!isLoadableAudio)
		{
			EditorGUILayout.HelpBox("Cannot get data on compressed samples. Change load type to DecompressOnLoad on the Audioclip", MessageType.Error);
			if (GUILayout.Button("Fix now", GUILayout.MaxWidth(80)))
			{
				string asset = AssetDatabase.GetAssetPath(audio);
				AudioImporter ai = AudioImporter.GetAtPath(asset) as AudioImporter;
				AudioImporterSampleSettings settings = ai.defaultSampleSettings;
				settings.loadType = AudioClipLoadType.DecompressOnLoad;
				ai.defaultSampleSettings = settings;
				ai.SaveAndReimport();
			}
		}
		return isLoadableAudio;
	}
	IEnumerator CreateAnimation()
	{
		BitAnimatorWindow.SetupRuntimeAnimation(bitAnimator);
		repaint = BitAnimatorWindow.isRunningTask;
		updateAnimation = false;
		while (BitAnimatorWindow.isRunningTask)
		{
			Repaint();
			yield return null;
		}
	}
	IEnumerator WriteAnimation()
	{
		BitAnimatorWindow.WriteAnimation(bitAnimator);
		repaint = BitAnimatorWindow.isRunningTask;
		while (repaint)
		{
			Repaint();
			yield return null;
		}
	}
	void LoadOrCreateAnimationFile()
	{
		if (!String.IsNullOrEmpty(animationAsset.stringValue))
		{
			AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animationAsset.stringValue);
			if (clip == null)
			{
				clip = new AnimationClip();
				AssetDatabase.CreateAsset(clip, animationAsset.stringValue);
			}
			animationClip.objectReferenceValue = clip;
		}
	}
	void UpdateShaderProperties()
	{
		if (editorShader == null)
		{
			menu.AddDisabledItem(new GUIContent("Material"));
			return;
		}
		for (int i = 0; i < ShaderUtil.GetPropertyCount (editorShader); i++) 
		{
			ShaderUtil.ShaderPropertyType propertyType = ShaderUtil.GetPropertyType(editorShader, i);
			if (ShaderUtil.IsShaderPropertyHidden(editorShader, i) || propertyType == ShaderUtil.ShaderPropertyType.TexEnv)
				continue;

			BitAnimator.RecordSlot prop = new BitAnimator.RecordSlot ();
			BitAnimator.PropertyType type = (BitAnimator.PropertyType)(int)propertyType;
			string propertyName = ShaderUtil.GetPropertyName(editorShader, i);
			string propertyDescription = ShaderUtil.GetPropertyDescription(editorShader, i);
			prop.property = GetPropertyString(type, "material." + propertyName);
			prop.type = type;
			prop.typeSet = BitAnimator.RecordSlot.PropertiesSet.Material;
			prop.name = propertyDescription;
			prop.description = string.Format("Material/{0} ({1})", propertyDescription, propertyName);
			prop.icon = EditorGUIUtility.ObjectContent(null, typeof(Material)).image;
			prop.startFreq = 0;
			prop.endFreq = 120;
			if (prop.type == BitAnimator.PropertyType.Range)
			{
				prop.rangeMin = ShaderUtil.GetRangeLimits(editorShader, i, 1);
				prop.rangeMax = ShaderUtil.GetRangeLimits(editorShader, i, 2);
			}
			prop.channelMask = 0xFF;
			prop.loops = 1;
			availableVariables.Add (prop);
			menu.AddItem(new GUIContent(prop.description), false, AddProperty, prop);
		}
	}
	void UpdateParticleProperties()
	{
		if (particles == null)
		{
			menu.AddDisabledItem(new GUIContent("Particle System"));
			return;
		}
		var d = new []
		{
			new { name = "Main/looping", propertyName = "looping", type = BitAnimator.PropertyType.Int},
			new { name = "Main/simulation Speed", propertyName = "simulationSpeed", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Delay", propertyName = "startDelay", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Lifetime", propertyName = "InitialModule.startLifetime.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Lifetime (minimum)", propertyName = "InitialModule.startLifetime.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Speed", propertyName = "InitialModule.startSpeed.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Speed (minimum)", propertyName = "InitialModule.startSpeed.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Size", propertyName = "InitialModule.startSize.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Size Y", propertyName = "InitialModule.startSizeY.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Size Z", propertyName = "InitialModule.startSizeZ.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Size (minimum)", propertyName = "InitialModule.startSize.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Size Y (minimum)", propertyName = "InitialModule.startSizeY.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Size Z (minimum)", propertyName = "InitialModule.startSizeZ.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Rotation X", propertyName = "InitialModule.startRotationX.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Rotation Y", propertyName = "InitialModule.startRotationY.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Rotation", propertyName = "InitialModule.startRotation.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Rotation X (minimum)", propertyName = "InitialModule.startRotationX.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Rotation Y (minimum)", propertyName = "InitialModule.startRotationY.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Rotation (minimum)", propertyName = "InitialModule.startRotation.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/Start Color.minColor", propertyName = "InitialModule.startColor.minColor", type = BitAnimator.PropertyType.Color},
			new { name = "Main/Start Color.maxColor", propertyName = "InitialModule.startColor.maxColor", type = BitAnimator.PropertyType.Color},
			new { name = "Main/Randomize Rotation Direction", propertyName = "InitialModule.randomizeRotationDirection", type = BitAnimator.PropertyType.Float},
			new { name = "Main/gravity Modifier", propertyName = "InitialModule.gravityModifier.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Main/gravity Modifier (minimum)", propertyName = "InitialModule.gravityModifier.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Limit Velocity over Lifetime/Enabled", propertyName = "ClampVelocityModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Limit Velocity over Lifetime/X", propertyName = "ClampVelocityModule.x.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Limit Velocity over Lifetime/Y", propertyName = "ClampVelocityModule.y.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Limit Velocity over Lifetime/Z", propertyName = "ClampVelocityModule.z.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Limit Velocity over Lifetime/X (minimum)", propertyName = "ClampVelocityModule.x.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Limit Velocity over Lifetime/Y (minimum)", propertyName = "ClampVelocityModule.y.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Limit Velocity over Lifetime/Z (minimum)", propertyName = "ClampVelocityModule.z.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Limit Velocity over Lifetime/magnitude", propertyName = "ClampVelocityModule.magnitude.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Limit Velocity over Lifetime/magnitude (minimum)", propertyName = "ClampVelocityModule.magnitude.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Limit Velocity over Lifetime/dampen", propertyName = "ClampVelocityModule.dampen", type = BitAnimator.PropertyType.Float},
			new { name = "Limit Velocity over Lifetime/drag", propertyName = "ClampVelocityModule.drag.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Limit Velocity over Lifetime/drag (minimum)", propertyName = "ClampVelocityModule.drag.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Collision/Enabled", propertyName = "CollisionModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Collision/Dampen", propertyName = "CollisionModule.m_Dampen.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Collision/Dampen (minimum)", propertyName = "CollisionModule.m_Dampen.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Collision/Bounce", propertyName = "CollisionModule.m_Bounce.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Collision/Bounce (minimum)", propertyName = "CollisionModule.m_Bounce.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Collision/Energy Loss On Collision", propertyName = "CollisionModule.m_EnergyLossOnCollision.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Collision/Energy Loss On Collision (minimum)", propertyName = "CollisionModule.m_EnergyLossOnCollision.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Collision/min Kill Speed", propertyName = "CollisionModule.minKillSpeed", type = BitAnimator.PropertyType.Float},
			new { name = "Collision/max Kill Speed", propertyName = "CollisionModule.maxKillSpeed", type = BitAnimator.PropertyType.Float},
			new { name = "Collision/Collides With Dynamic", propertyName = "CollisionModule.collidesWithDynamic", type = BitAnimator.PropertyType.Float},
			new { name = "Collision/Radius Scale", propertyName = "CollisionModule.radiusScale", type = BitAnimator.PropertyType.Float},
			new { name = "Collision/Collider Force", propertyName = "CollisionModule.colliderForce", type = BitAnimator.PropertyType.Float},
			new { name = "Color by Speed/Enabled", propertyName = "ColorBySpeedModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Color by Speed/gradient.minColor", propertyName = "ColorBySpeedModule.gradient.minColor", type = BitAnimator.PropertyType.Color},
			new { name = "Color by Speed/gradient.maxColor", propertyName = "ColorBySpeedModule.gradient.maxColor", type = BitAnimator.PropertyType.Color},
			new { name = "Color by Speed/Range.x", propertyName = "ColorBySpeedModule.range.x", type = BitAnimator.PropertyType.Float},
			new { name = "Color by Speed/Range.y", propertyName = "ColorBySpeedModule.range.y", type = BitAnimator.PropertyType.Float},
			new { name = "Color over Lifetime/Enabled", propertyName = "ColorModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Color over Lifetime/gradient.minColor", propertyName = "ColorModule.gradient.minColor", type = BitAnimator.PropertyType.Color},
			new { name = "Color over Lifetime/gradient.maxColor", propertyName = "ColorModule.gradient.maxColor", type = BitAnimator.PropertyType.Color},
			new { name = "Custom Data/Enabled", propertyName = "CustomDataModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Custom Data/vector0_0", propertyName = "CustomDataModule.vector0_0.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector0_0 (minimum)", propertyName = "CustomDataModule.vector0_0.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector0_1", propertyName = "CustomDataModule.vector0_1.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector0_1 (minimum)", propertyName = "CustomDataModule.vector0_1.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector0_2", propertyName = "CustomDataModule.vector0_2.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector0_2 (minimum)", propertyName = "CustomDataModule.vector0_2.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector0_3", propertyName = "CustomDataModule.vector0_3.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector0_3 (minimum)", propertyName = "CustomDataModule.vector0_3.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector1_0", propertyName = "CustomDataModule.vector1_0.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector1_0 (minimum)", propertyName = "CustomDataModule.vector1_0.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector1_1", propertyName = "CustomDataModule.vector1_1.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector1_1 (minimum)", propertyName = "CustomDataModule.vector1_1.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector1_2", propertyName = "CustomDataModule.vector1_2.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector1_2 (minimum)", propertyName = "CustomDataModule.vector1_2.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector1_3", propertyName = "CustomDataModule.vector1_3.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/vector1_3 (minimum)", propertyName = "CustomDataModule.vector1_3.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Custom Data/Color0.minColor", propertyName = "CustomDataModule.color0.minColor", type = BitAnimator.PropertyType.Color},
			new { name = "Custom Data/Color0.maxColor", propertyName = "CustomDataModule.color0.maxColor", type = BitAnimator.PropertyType.Color},
			new { name = "Custom Data/Color1.minColor", propertyName = "CustomDataModule.color1.minColor", type = BitAnimator.PropertyType.Color},
			new { name = "Custom Data/Color1.maxColor", propertyName = "CustomDataModule.color1.maxColor", type = BitAnimator.PropertyType.Color},
			new { name = "External Forces/Enabled", propertyName = "ExternalForcesModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "External Forces/multiplier", propertyName = "ExternalForcesModule.multiplier", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Enabled", propertyName = "EmissionModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Emission/Rate over time", propertyName = "EmissionModule.rateOverTime.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Rate over time (minimum)", propertyName = "EmissionModule.rateOverTime.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Rate over distance", propertyName = "EmissionModule.rateOverDistance.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Rate over distance (minimum)", propertyName = "EmissionModule.rateOverDistance.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[0]/Count Curve", propertyName = "EmissionModule.m_Bursts.Array.data[0].countCurve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[0]/Count Curve (minimum)", propertyName = "EmissionModule.m_Bursts.Array.data[0].countCurve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[1]/Count Curve", propertyName = "EmissionModule.m_Bursts.Array.data[1].countCurve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[1]/Count Curve (minimum)", propertyName = "EmissionModule.m_Bursts.Array.data[1].countCurve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[2]/Count Curve", propertyName = "EmissionModule.m_Bursts.Array.data[2].countCurve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[2]/Count Curve (minimum)", propertyName = "EmissionModule.m_Bursts.Array.data[2].countCurve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[3]/Count Curve", propertyName = "EmissionModule.m_Bursts.Array.data[3].countCurve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[3]/Count Curve (minimum)", propertyName = "EmissionModule.m_Bursts.Array.data[3].countCurve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[4]/Count Curve", propertyName = "EmissionModule.m_Bursts.Array.data[4].countCurve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[4]/Count Curve (minimum)", propertyName = "EmissionModule.m_Bursts.Array.data[4].countCurve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[5]/Count Curve", propertyName = "EmissionModule.m_Bursts.Array.data[5].countCurve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[5]/Count Curve (minimum)", propertyName = "EmissionModule.m_Bursts.Array.data[5].countCurve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[6]/Count Curve", propertyName = "EmissionModule.m_Bursts.Array.data[6].countCurve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[6]/Count Curve (minimum)", propertyName = "EmissionModule.m_Bursts.Array.data[6].countCurve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[7]/Count Curve", propertyName = "EmissionModule.m_Bursts.Array.data[7].countCurve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[7]/Count Curve (minimum)", propertyName = "EmissionModule.m_Bursts.Array.data[7].countCurve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[0]/time", propertyName = "EmissionModule.m_Bursts.Array.data[0].time", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[1]/time", propertyName = "EmissionModule.m_Bursts.Array.data[1].time", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[2]/time", propertyName = "EmissionModule.m_Bursts.Array.data[2].time", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[3]/time", propertyName = "EmissionModule.m_Bursts.Array.data[3].time", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[4]/time", propertyName = "EmissionModule.m_Bursts.Array.data[4].time", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[5]/time", propertyName = "EmissionModule.m_Bursts.Array.data[5].time", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[6]/time", propertyName = "EmissionModule.m_Bursts.Array.data[6].time", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[7]/time", propertyName = "EmissionModule.m_Bursts.Array.data[7].time", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[0]/Cycle Count", propertyName = "EmissionModule.m_Bursts.Array.data[0].cycleCount", type = BitAnimator.PropertyType.Int},
			new { name = "Emission/Burst[1]/Cycle Count", propertyName = "EmissionModule.m_Bursts.Array.data[1].cycleCount", type = BitAnimator.PropertyType.Int},
			new { name = "Emission/Burst[2]/Cycle Count", propertyName = "EmissionModule.m_Bursts.Array.data[2].cycleCount", type = BitAnimator.PropertyType.Int},
			new { name = "Emission/Burst[3]/Cycle Count", propertyName = "EmissionModule.m_Bursts.Array.data[3].cycleCount", type = BitAnimator.PropertyType.Int},
			new { name = "Emission/Burst[4]/Cycle Count", propertyName = "EmissionModule.m_Bursts.Array.data[4].cycleCount", type = BitAnimator.PropertyType.Int},
			new { name = "Emission/Burst[5]/Cycle Count", propertyName = "EmissionModule.m_Bursts.Array.data[5].cycleCount", type = BitAnimator.PropertyType.Int},
			new { name = "Emission/Burst[6]/Cycle Count", propertyName = "EmissionModule.m_Bursts.Array.data[6].cycleCount", type = BitAnimator.PropertyType.Int},
			new { name = "Emission/Burst[7]/Cycle Count", propertyName = "EmissionModule.m_Bursts.Array.data[7].cycleCount", type = BitAnimator.PropertyType.Int},
			new { name = "Emission/Burst[0]/Repeat Interval", propertyName = "EmissionModule.m_Bursts.Array.data[0].repeatInterval", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[1]/Repeat Interval", propertyName = "EmissionModule.m_Bursts.Array.data[1].repeatInterval", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[2]/Repeat Interval", propertyName = "EmissionModule.m_Bursts.Array.data[2].repeatInterval", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[3]/Repeat Interval", propertyName = "EmissionModule.m_Bursts.Array.data[3].repeatInterval", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[4]/Repeat Interval", propertyName = "EmissionModule.m_Bursts.Array.data[4].repeatInterval", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[5]/Repeat Interval", propertyName = "EmissionModule.m_Bursts.Array.data[5].repeatInterval", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[6]/Repeat Interval", propertyName = "EmissionModule.m_Bursts.Array.data[6].repeatInterval", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst[7]/Repeat Interval", propertyName = "EmissionModule.m_Bursts.Array.data[7].repeatInterval", type = BitAnimator.PropertyType.Float},
			new { name = "Emission/Burst count", propertyName = "EmissionModule.m_BurstCount", type = BitAnimator.PropertyType.Float},
			new { name = "Force over Lifetime/Enabled", propertyName = "ForceModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Force over Lifetime/X", propertyName = "ForceModule.x.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Force over Lifetime/Y", propertyName = "ForceModule.y.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Force over Lifetime/Z", propertyName = "ForceModule.z.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Force over Lifetime/X (minimum)", propertyName = "ForceModule.x.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Force over Lifetime/Y (minimum)", propertyName = "ForceModule.y.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Force over Lifetime/Z (minimum)", propertyName = "ForceModule.z.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Force over Lifetime/Randomize Per Frame", propertyName = "ForceModule.randomizePerFrame", type = BitAnimator.PropertyType.Float},
			new { name = "Inherit Velocity/Enabled", propertyName = "InheritVelocityModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Inherit Velocity/Curve", propertyName = "InheritVelocityModule.m_Curve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Inherit Velocity/Curve (minimum)", propertyName = "InheritVelocityModule.m_Curve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Lights/Enabled", propertyName = "LightsModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Lights/Ratio", propertyName = "LightsModule.ratio", type = BitAnimator.PropertyType.Float},
			new { name = "Lights/Random Distribution", propertyName = "LightsModule.randomDistribution", type = BitAnimator.PropertyType.Float},
			new { name = "Lights/use Particle Color", propertyName = "LightsModule.useParticleColor", type = BitAnimator.PropertyType.Float},
			new { name = "Lights/size Affects Range", propertyName = "LightsModule.sizeAffectsRange", type = BitAnimator.PropertyType.Float},
			new { name = "Lights/Alpha Affects Intensity", propertyName = "LightsModule.alphaAffectsIntensity", type = BitAnimator.PropertyType.Float},
			new { name = "Lights/Range Curve", propertyName = "LightsModule.rangeCurve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Lights/Range Curve (minimum)", propertyName = "LightsModule.rangeCurve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Lights/intensity Curve", propertyName = "LightsModule.intensityCurve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Lights/intensity Curve (minimum)", propertyName = "LightsModule.intensityCurve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Enabled", propertyName = "NoiseModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Noise/strength", propertyName = "NoiseModule.strength.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/strength Y", propertyName = "NoiseModule.strengthY.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/strength Z", propertyName = "NoiseModule.strengthZ.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/strength (minimum)", propertyName = "NoiseModule.strength.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/strength Y (minimum)", propertyName = "NoiseModule.strengthY.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/strength Z (minimum)", propertyName = "NoiseModule.strengthZ.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/frequency", propertyName = "NoiseModule.frequency", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/damping", propertyName = "NoiseModule.damping", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/octave Multiplier", propertyName = "NoiseModule.octaveMultiplier", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/octave Scale", propertyName = "NoiseModule.octaveScale", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/scroll Speed", propertyName = "NoiseModule.scrollSpeed.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/scroll Speed (minimum)", propertyName = "NoiseModule.scrollSpeed.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Remap", propertyName = "NoiseModule.remap.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Remap Y", propertyName = "NoiseModule.remapY.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Remap Z", propertyName = "NoiseModule.remapZ.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Remap (minimum)", propertyName = "NoiseModule.remap.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Remap Y (minimum)", propertyName = "NoiseModule.remapY.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Remap Z (minimum)", propertyName = "NoiseModule.remapZ.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Position Amount", propertyName = "NoiseModule.positionAmount.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Position Amount (minimum)", propertyName = "NoiseModule.positionAmount.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Rotation Amount", propertyName = "NoiseModule.rotationAmount.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Rotation Amount (minimum)", propertyName = "NoiseModule.rotationAmount.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Size Amount", propertyName = "NoiseModule.sizeAmount.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Noise/Size Amount (minimum)", propertyName = "NoiseModule.sizeAmount.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation by Speed/Enabled", propertyName = "RotationBySpeedModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Rotation by Speed/X", propertyName = "RotationBySpeedModule.x.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation by Speed/Y", propertyName = "RotationBySpeedModule.y.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation by Speed/Curve", propertyName = "RotationBySpeedModule.curve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation by Speed/X (minimum)", propertyName = "RotationBySpeedModule.x.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation by Speed/Y (minimum)", propertyName = "RotationBySpeedModule.y.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation by Speed/Curve (minimum)", propertyName = "RotationBySpeedModule.curve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation by Speed/Range.X", propertyName = "RotationBySpeedModule.range.x", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation by Speed/Range.Y", propertyName = "RotationBySpeedModule.range.y", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation over Lifetime/Enabled", propertyName = "RotationModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Rotation over Lifetime/X", propertyName = "RotationModule.x.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation over Lifetime/Y", propertyName = "RotationModule.y.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation over Lifetime/Curve", propertyName = "RotationModule.curve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation over Lifetime/X (minimum)", propertyName = "RotationModule.x.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation over Lifetime/Y (minimum)", propertyName = "RotationModule.y.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Rotation over Lifetime/Curve (minimum)", propertyName = "RotationModule.curve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Enabled", propertyName = "ShapeModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Shape/Radius.value", propertyName = "ShapeModule.radius.value", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Radius.spread", propertyName = "ShapeModule.radius.spread", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Radius.speed", propertyName = "ShapeModule.radius.speed.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Radius.speed (minimum)", propertyName = "ShapeModule.radius.speed.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/RadiusThickness", propertyName = "ShapeModule.radiusThickness", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Angle", propertyName = "ShapeModule.angle", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/length", propertyName = "ShapeModule.length", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Box Thickness", propertyName = "ShapeModule.boxThickness", type = BitAnimator.PropertyType.Vector3},
			new { name = "Shape/Arc.value", propertyName = "ShapeModule.arc.value", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Arc.spread", propertyName = "ShapeModule.arc.spread", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Arc.speed", propertyName = "ShapeModule.arc.speed.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Arc.speed (minimum)", propertyName = "ShapeModule.arc.speed.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Mesh Material Index", propertyName = "ShapeModule.m_MeshMaterialIndex", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Mesh Normal Offset", propertyName = "ShapeModule.m_MeshNormalOffset", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Align To Direction", propertyName = "ShapeModule.alignToDirection", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Random Direction Amount", propertyName = "ShapeModule.randomDirectionAmount", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Spherical Direction Amount", propertyName = "ShapeModule.sphericalDirectionAmount", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Random Position Amount", propertyName = "ShapeModule.randomPositionAmount", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Use Mesh Material Index", propertyName = "ShapeModule.m_UseMeshMaterialIndex", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Use Mesh Colors", propertyName = "ShapeModule.m_UseMeshColors", type = BitAnimator.PropertyType.Float},
			new { name = "Shape/Position", propertyName = "ShapeModule.m_Position", type = BitAnimator.PropertyType.Vector3},
			new { name = "Shape/Rotation", propertyName = "ShapeModule.m_Rotation", type = BitAnimator.PropertyType.Vector3},
			new { name = "Shape/Scale", propertyName = "ShapeModule.m_Scale", type = BitAnimator.PropertyType.Vector3},
			new { name = "Size by Speed/Enabled", propertyName = "SizeBySpeedModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Size by Speed/Curve", propertyName = "SizeBySpeedModule.curve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Size by Speed/Y", propertyName = "SizeBySpeedModule.y.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Size by Speed/Z", propertyName = "SizeBySpeedModule.z.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Size by Speed/Curve (minimum)", propertyName = "SizeBySpeedModule.curve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Size by Speed/Y (minimum)", propertyName = "SizeBySpeedModule.y.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Size by Speed/Z (minimum)", propertyName = "SizeBySpeedModule.z.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Size by Speed/Range.x", propertyName = "SizeBySpeedModule.range.x", type = BitAnimator.PropertyType.Float},
			new { name = "Size by Speed/Range.y", propertyName = "SizeBySpeedModule.range.y", type = BitAnimator.PropertyType.Float},
			new { name = "Size over Lifetime/Enabled", propertyName = "SizeModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Size over Lifetime/Curve", propertyName = "SizeModule.curve.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Size over Lifetime/Y", propertyName = "SizeModule.y.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Size over Lifetime/Z", propertyName = "SizeModule.z.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Size over Lifetime/Curve (minimum)", propertyName = "SizeModule.curve.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Size over Lifetime/Y (minimum)", propertyName = "SizeModule.y.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Size over Lifetime/Z (minimum)", propertyName = "SizeModule.z.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Trails/Enabled", propertyName = "TrailModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Trails/Ratio", propertyName = "TrailModule.ratio", type = BitAnimator.PropertyType.Float},
			new { name = "Trails/Lifetime", propertyName = "TrailModule.lifetime.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Trails/Lifetime (minimum)", propertyName = "TrailModule.lifetime.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Trails/Min Vertex Distance", propertyName = "TrailModule.minVertexDistance", type = BitAnimator.PropertyType.Float},
			new { name = "Trails/Die With Particles", propertyName = "TrailModule.dieWithParticles", type = BitAnimator.PropertyType.Float},
			new { name = "Trails/Size Affects Width", propertyName = "TrailModule.sizeAffectsWidth", type = BitAnimator.PropertyType.Float},
			new { name = "Trails/Size Affects Lifetime", propertyName = "TrailModule.sizeAffectsLifetime", type = BitAnimator.PropertyType.Float},
			new { name = "Trails/Inherit Particle Color", propertyName = "TrailModule.inheritParticleColor", type = BitAnimator.PropertyType.Float},
			new { name = "Trails/Color Over Lifetime.minColor", propertyName = "TrailModule.colorOverLifetime.minColor", type = BitAnimator.PropertyType.Color},
			new { name = "Trails/Color Over Lifetime.maxColor", propertyName = "TrailModule.colorOverLifetime.maxColor", type = BitAnimator.PropertyType.Color},
			new { name = "Trails/Width Over Trail", propertyName = "TrailModule.widthOverTrail.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Trails/Width Over Trail (minimum)", propertyName = "TrailModule.widthOverTrail.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Trails/Color Over Trail.minColor", propertyName = "TrailModule.colorOverTrail.minColor", type = BitAnimator.PropertyType.Color},
			new { name = "Trails/Color Over Trail.maxColor", propertyName = "TrailModule.colorOverTrail.maxColor", type = BitAnimator.PropertyType.Color},
			new { name = "Triggers/Enabled", propertyName = "TriggerModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Triggers/Radius Scale", propertyName = "TriggerModule.radiusScale", type = BitAnimator.PropertyType.Float},
			new { name = "Texture Sheet Animation/Enabled", propertyName = "UVModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Texture Sheet Animation/Frame Over Time", propertyName = "UVModule.frameOverTime.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Texture Sheet Animation/Frame Over Time (minimum)", propertyName = "UVModule.frameOverTime.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Texture Sheet Animation/Start Frame", propertyName = "UVModule.startFrame.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Texture Sheet Animation/Start Frame (minimum)", propertyName = "UVModule.startFrame.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Velocity over Lifetime/Enabled", propertyName = "VelocityModule.enabled", type = BitAnimator.PropertyType.Int},
			new { name = "Velocity over Lifetime/X", propertyName = "VelocityModule.x.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Velocity over Lifetime/Y", propertyName = "VelocityModule.y.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Velocity over Lifetime/Z", propertyName = "VelocityModule.z.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Velocity over Lifetime/X (minimum)", propertyName = "VelocityModule.x.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Velocity over Lifetime/Y (minimum)", propertyName = "VelocityModule.y.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Velocity over Lifetime/Z (minimum)", propertyName = "VelocityModule.z.minScalar", type = BitAnimator.PropertyType.Float},
			new { name = "Velocity over Lifetime/Speed Modifier", propertyName = "VelocityModule.speedModifier.scalar", type = BitAnimator.PropertyType.Float},
			new { name = "Velocity over Lifetime/Speed Modifier (minimum)", propertyName = "VelocityModule.speedModifier.minScalar", type = BitAnimator.PropertyType.Float}
		};
		foreach (var particleVariable in d) 
		{
			BitAnimator.RecordSlot prop = new BitAnimator.RecordSlot ();
			prop.property = GetPropertyString(particleVariable.type, particleVariable.propertyName);
			prop.type = particleVariable.type;
			prop.typeSet = BitAnimator.RecordSlot.PropertiesSet.ParticleSystem;
			prop.name = particleVariable.name;
			prop.description = "Particle System/" + particleVariable.name;
			prop.icon = EditorGUIUtility.ObjectContent(null, typeof(ParticleSystem)).image;
			prop.startFreq = 0;
			prop.endFreq = audio.frequency / 2;
			prop.channelMask = 0xFF;
			prop.loops = 1;
			availableVariables.Add (prop);
			menu.AddItem(new GUIContent(prop.description), false, AddProperty, prop);
		}
	}
	void UpdateTransformProperties()
	{
		var d = new []
		{
			new { name = "Position", propertyName = "localPosition", type = BitAnimator.PropertyType.Vector3, minValue = Vector3.zero, maxValue = Vector3.zero},
			new { name = "Rotation", propertyName = "localRotation", type = BitAnimator.PropertyType.Quaternion, minValue = Vector3.zero, maxValue = Vector3.zero},
			new { name = "Scale", propertyName = "localScale", type = BitAnimator.PropertyType.Vector3, minValue = Vector3.one, maxValue = Vector3.one},
		};
		foreach (var transformVariable in d) 
		{
			BitAnimator.RecordSlot prop = new BitAnimator.RecordSlot ();
			prop.property = GetPropertyString(transformVariable.type, transformVariable.propertyName);
			prop.type = transformVariable.type;
			prop.typeSet = BitAnimator.RecordSlot.PropertiesSet.Transform;
			prop.name = transformVariable.name;
			prop.description = "Transform/" + transformVariable.name;
			prop.icon = EditorGUIUtility.ObjectContent(null, typeof(Transform)).image;
			prop.startFreq = 0;
			prop.endFreq = 300;
			prop.minValue = transformVariable.minValue;
			prop.maxValue = transformVariable.maxValue;
			prop.channelMask = 0xFF;
			prop.loops = 1;
			availableVariables.Add (prop);
			menu.AddItem(new GUIContent(prop.description), false, AddProperty, prop);
		}
	}
	void UpdateBlendShapeProperties() 
	{
		SkinnedMeshRenderer skinMeshObj = renderer as SkinnedMeshRenderer;
		if (skinMeshObj == null)
		{
			menu.AddDisabledItem(new GUIContent("Blend Shapes"));
			return;
		}
		Mesh blendShapeMesh = skinMeshObj.sharedMesh;
		int blendShapeCount = blendShapeMesh.blendShapeCount;
		for (int i = 0; i < blendShapeCount; i++) 
		{
			string blendShapeName = blendShapeMesh.GetBlendShapeName(i);
			BitAnimator.RecordSlot prop = new BitAnimator.RecordSlot();
			prop.property = new string[1] { "blendShape." + blendShapeName };
			prop.type = BitAnimator.PropertyType.Float;
			prop.typeSet = BitAnimator.RecordSlot.PropertiesSet.BlendShape;
			prop.name = blendShapeName;
			prop.description = "Blend Shapes/" + blendShapeName;
			prop.icon = EditorGUIUtility.ObjectContent(null, typeof(SkinnedMeshRenderer)).image;
			prop.startFreq = 500;
			prop.endFreq = 1000;
			prop.rangeMin = 0;
			prop.rangeMax = 100;
			prop.channelMask = 0xFF;
			prop.loops = 1;
			availableVariables.Add(prop);
			menu.AddItem(new GUIContent(prop.description), false, AddProperty, prop);
		}
	}
	class PropertyBinding
	{
		[Flags]
		internal enum Channel
		{
			X = (1 << 0),
			Y = (1 << 1),
			Z = (1 << 2),
			W = (1 << 3),
			R = (1 << 4),
			G = (1 << 5),
			B = (1 << 6),
			A = (1 << 7),
			XY = X | Y,
			XYZ = X | Y | Z,
			XYZW = X | Y | Z | W,
			RGB = R | G | B,
			RGBA = R | G | B | A,
		}
		internal string path;
		internal string property;
		internal Type type;
		internal Channel mask;
		internal bool isDiscrete;
	}
	void UpdateCustomProperties()
	{
		EditorCurveBinding[] ecbs = AnimationUtility.GetAnimatableBindings(targetGO, animatorGO.gameObject);
		Dictionary<string, PropertyBinding> properties = new Dictionary<string, PropertyBinding>(ecbs.Length * 4);
		foreach (EditorCurveBinding ecb in ecbs)
		{
			if (ecb.isPPtrCurve || ecb.type == typeof(Transform) || ecb.type == typeof(ParticleSystem))
				continue;

			string propName = ecb.propertyName;
			if (propName.StartsWith("material.") || propName.StartsWith("blendShape."))
				continue;

			PropertyBinding.Channel channel = 0;
			channel |= propName.EndsWith(".x") ? PropertyBinding.Channel.X : 0;
			channel |= propName.EndsWith(".y") ? PropertyBinding.Channel.Y : 0;
			channel |= propName.EndsWith(".z") ? PropertyBinding.Channel.Z : 0;
			channel |= propName.EndsWith(".w") ? PropertyBinding.Channel.W : 0;
			channel |= propName.EndsWith(".r") ? PropertyBinding.Channel.R : 0;
			channel |= propName.EndsWith(".g") ? PropertyBinding.Channel.G : 0;
			channel |= propName.EndsWith(".b") ? PropertyBinding.Channel.B : 0;
			channel |= propName.EndsWith(".a") ? PropertyBinding.Channel.A : 0;

			string key;
			if (channel > 0)
			{
				propName = propName.Substring(0, propName.Length - 2);
				key = ecb.type.FullName + propName;
				PropertyBinding property;
				if (properties.TryGetValue(key, out property))
				{
					property.mask |= channel;
					continue;
				}
			}
			else
			{
				key = ecb.type.FullName + propName;
			}
			properties.Add(key, new PropertyBinding() 
			{ 
				path = ecb.path, 
				property = propName, 
				type = ecb.type, 
				mask = channel, 
				isDiscrete = ecb.isDiscreteCurve 
			});
		}
		foreach (PropertyBinding binding in properties.Values)
		{
			bool isColor = (binding.mask & PropertyBinding.Channel.RGBA) != 0;
			bool isVector4 = (binding.mask & PropertyBinding.Channel.XYZW) != 0;
			bool isVector3 = (binding.mask & PropertyBinding.Channel.XYZ) != 0;

			BitAnimator.PropertyType type;
			if (isVector4)
				type = BitAnimator.PropertyType.Vector;
			else if (isVector3)
				type = BitAnimator.PropertyType.Vector3;
			else if (isColor)
				type = BitAnimator.PropertyType.Color;
			else if(binding.isDiscrete)
				type = BitAnimator.PropertyType.Int;
			else
				type = BitAnimator.PropertyType.Float;

			if (isColor && isVector4)
				Debug.LogError("[BitAnimatorEditor] unknown property type: " + binding.property);

			BitAnimator.RecordSlot prop = new BitAnimator.RecordSlot();
			prop.property = GetPropertyString(type, binding.property);
			prop.type = type;
			prop.typeSet = BitAnimator.RecordSlot.PropertiesSet.Custom;
			prop.customTypeFullName = binding.type.AssemblyQualifiedName;
			prop.name = binding.property;
			prop.description = binding.type.Name + "/" + binding.property;
			prop.icon = EditorGUIUtility.ObjectContent(null, binding.type).image;
			prop.startFreq = 0;
			prop.endFreq = 100;
			prop.channelMask = 0xFF;
			prop.loops = 1;
			availableVariables.Add(prop);
			menu.AddItem(new GUIContent(prop.description), false, AddProperty, prop);
		}
	}
	string[] GetPropertyString(BitAnimator.PropertyType type, string name)
	{
		string[] prop = null;
		switch (type)
		{
			case BitAnimator.PropertyType.Int:
			case BitAnimator.PropertyType.Float:
			case BitAnimator.PropertyType.Range:
			case BitAnimator.PropertyType.TexEnv:
				prop = new string[1];
				prop[0] = name;
				break;
			case BitAnimator.PropertyType.Vector:
			case BitAnimator.PropertyType.Quaternion:
				prop = new string[4];
				prop[0] = name + ".x";
				prop[1] = name + ".y";
				prop[2] = name + ".z";
				prop[3] = name + ".w";
				break;
			case BitAnimator.PropertyType.Color:
				prop = new string[4];
				prop[0] = name + ".r";
				prop[1] = name + ".g";
				prop[2] = name + ".b";
				prop[3] = name + ".a";
				break;
			case BitAnimator.PropertyType.Vector3:
				prop = new string[3];
				prop[0] = name + ".x";
				prop[1] = name + ".y";
				prop[2] = name + ".z";
				break;
		}
		return prop;
	}
	static GUIContent bpmLabel = new GUIContent("BPM", "Beats Per Minute\nAffects on triggers/bursts properties\nTo calc this value automatically set -1");
	static GUIContent offsetLabel = new GUIContent("Offset", "Beat offset in milliseconds");
	static GUIContent setBurstsLabel = new GUIContent("Set Bursts in Particle System", "");
	static GUIContent recalcBpmLabel = null;
	static GUIContent clearTempAnimationLabel = new GUIContent("Clear temp animation clip", "Delete all animation curves in temporary AnimationClip");
	static GUIContent clearAnimationLabel = new GUIContent("Clear animation clip", "Delete all animation curves in AnimationClip");
	void RenderAdvancedSettings()
	{
		//string[] displayedPresets = presets.Select(p => p.name).ToArray();
		//int selectedPreset = Array.IndexOf(displayedPresets, presetName.stringValue);
		using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
		{
			using (new EditorGUI.IndentLevelScope(+1))
			{
				advancedSettingsToggle = EditorGUILayout.Foldout(advancedSettingsToggle, "Settings", true);
				using (var advancedProperties = new EditorGUI.ChangeCheckScope())
				{
					if (advancedSettingsToggle)
					{
						DrawInterfaceMode();
						/*EditorGUILayout.BeginHorizontal();
						selectedPreset = EditorGUILayout.Popup("Preset", selectedPreset, displayedPresets);
						if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus", "Add preset"), EditorStyles.miniButtonLeft, GUILayout.MaxWidth(24), GUILayout.MaxHeight(16)))
						{
						}
						if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus", "Remove preset"), EditorStyles.miniButtonRight, GUILayout.MaxWidth(24), GUILayout.MaxHeight(16)))
						{
						}
						EditorGUILayout.EndHorizontal();*/
						/*
						string engine = selectedEngine.stringValue;
						if (String.IsNullOrEmpty(engine))
							selectedEngine.stringValue = engine = keyframingEngines[0].typeDescriptor;
						*/
						if (BitAnimator.interfaceMode >= BitAnimator.InterfaceMode.Default)
						{
							/*string[] displayedEngines = keyframingEngines.Select(p => p.name).ToArray();
							int engineIndex = keyframingEngines.FindIndex(e => e.typeDescriptor == engine);
							engineIndex = EditorGUILayout.Popup("Engine", engineIndex, displayedEngines);
							selectedEngine.stringValue = keyframingEngines[engineIndex].typeDescriptor;
							*/
							EditorGUILayout.PropertyField(engineType);
							using (new EditorGUILayout.HorizontalScope())
							{
								float w = EditorGUIUtility.labelWidth;
								EditorGUIUtility.labelWidth = 50;
								EditorGUILayout.PrefixLabel(bpmLabel);
								bpm.intValue = EditorGUILayout.IntField(bpm.intValue);
								using (new EditorGUI.IndentLevelScope(-1))
								{
									EditorGUILayout.PrefixLabel(offsetLabel);
									beatOffset.floatValue = EditorGUILayout.Slider(beatOffset.floatValue, 0, 60000.0f / bpm.intValue);
									//beatOffset.floatValue = EditorGUILayout.FloatField(beatOffset.floatValue);
									if (GUILayout.Button(recalcBpmLabel, testStyle))
									{
										EditorCoroutines.Start(CalculateBPM());
									}
								}
								EditorGUIUtility.labelWidth = w;
							}
							if (particles != null)
							{
								if (GUILayout.Button(setBurstsLabel))
								{
									bool wasPlaying = particles.isPlaying;
									particles.Stop();
									ParticleSystem.MainModule main = particles.main;
									main.duration = audio.length;

									ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[1];
									bursts[0].time = beatOffset.floatValue / 1000.0f;
									bursts[0].cycleCount = 0;
									bursts[0].count = 100;
									bursts[0].repeatInterval = 60.0f / bpm.intValue;
									particles.emission.SetBursts(bursts);
									if (wasPlaying)
										particles.Play();
								}
							}
							if (BitAnimator.interfaceMode >= BitAnimator.InterfaceMode.Expert)
							{
								Engine.maxUseRAM = EditorGUILayout.IntSlider("RAM usage", Engine.maxUseRAM, 32, SystemInfo.systemMemorySize / 2);
								Engine.maxUseVRAM = EditorGUILayout.IntSlider("VRAM usage", Engine.maxUseVRAM, 32, SystemInfo.graphicsMemorySize / 2);
							}
						}
						bitAnimator.engineSettings[(int)bitAnimator.engineType].DrawProperty();
						EditorGUILayout.PropertyField(plan);
						GUILayout.Space(8);

						Color oldBackground = GUI.backgroundColor;
						using (new EditorGUILayout.HorizontalScope())
						{
							GUILayout.FlexibleSpace();
							if (BitAnimatorWindow.animation != null)
							{
								if (GUILayout.Button(clearTempAnimationLabel, GUILayout.MaxWidth(180)))
									BitAnimatorWindow.ClearRunTimeClip();
							}

							GUI.backgroundColor = Color.Lerp(GUI.backgroundColor, Color.red, 0.25f);
							AnimationClip clip = animationClip.objectReferenceValue as AnimationClip;
							if (clip != null && !clip.empty && GUILayout.Button(clearAnimationLabel))
								bitAnimator.ClearAnimation();
						}
						GUI.backgroundColor = oldBackground;
					}
					if (advancedProperties.changed)
					{
						updateAnimation = true;
						if (BitAnimatorWindow.instance != null)
						{
							serializedObject.ApplyModifiedProperties();
							BitAnimatorWindow.instance.InitializeEngine();
						}
					}
				}
			}
		}
	}
	protected static void DrawInterfaceMode()
	{
		EditorGUI.BeginChangeCheck();
		BitAnimator.interfaceMode = (BitAnimator.InterfaceMode)EditorGUILayout.EnumPopup(new GUIContent("Interface mode", "How much details do you want to control"), BitAnimator.interfaceMode);
		if(EditorGUI.EndChangeCheck())
		{
			EditorPrefs.SetInt("BitAnimator.InterfaceMode", (int)BitAnimator.interfaceMode);
		}
	}
	IEnumerator CalculateBPM()
	{
		yield return null;
		bitAnimator.InitializeEngine();
		IEnumerator i = bitAnimator.engine.ComputeBPM();
		while (i.MoveNext())
		{
			EditorUtility.DisplayProgressBar("Calculating BPM", bitAnimator.engine.Status, bitAnimator.engine.Progress);
			yield return i.Current;
		}
		bitAnimator.bpm = bitAnimator.engine.bpm;
		bitAnimator.beatOffset = bitAnimator.engine.beatOffset;
		EditorUtility.ClearProgressBar();
	}
	void AddProperty(object obj)
	{
		BitAnimator.RecordSlot slot = obj as BitAnimator.RecordSlot;
		if(bitAnimator.recordSlots.Any(s => s.property[0] == slot.property[0] && s.customTypeFullName == slot.customTypeFullName))
		{
			throw new DublicateException("BitAnimator already contains " + slot.description);
		}
		++recordSlots.arraySize;
		SerializedProperty serializedProp = recordSlots.GetArrayElementAtIndex(recordSlots.arraySize - 1);
		SerializedProperty icon = serializedProp.FindPropertyRelative("icon");
		SerializedProperty resolver = serializedProp.FindPropertyRelative("resolver");
		SerializedProperty name = serializedProp.FindPropertyRelative("name");
		SerializedProperty property = serializedProp.FindPropertyRelative("property");
		SerializedProperty type = serializedProp.FindPropertyRelative("type");
		SerializedProperty typeSet = serializedProp.FindPropertyRelative("typeSet");
		SerializedProperty customTypeFullName = serializedProp.FindPropertyRelative("customTypeFullName");
		SerializedProperty description = serializedProp.FindPropertyRelative("description");
		SerializedProperty startFreq = serializedProp.FindPropertyRelative("startFreq");
		SerializedProperty endFreq = serializedProp.FindPropertyRelative("endFreq");
		SerializedProperty minValue = serializedProp.FindPropertyRelative("minValue");
		SerializedProperty maxValue = serializedProp.FindPropertyRelative("maxValue");
		SerializedProperty rangeMin = serializedProp.FindPropertyRelative("rangeMin");
		SerializedProperty rangeMax = serializedProp.FindPropertyRelative("rangeMax");
		//SerializedProperty colors = serializedProp.FindPropertyRelative("colors");
		SerializedProperty channelMask = serializedProp.FindPropertyRelative("channelMask");
		SerializedProperty loops = serializedProp.FindPropertyRelative("loops");
		SerializedProperty modificators = serializedProp.FindPropertyRelative("modificators");
		//if (name.stringValue != slot.name)
		{
			type.intValue = (int)slot.type;
			typeSet.intValue = (int)slot.typeSet;
			customTypeFullName.stringValue = slot.customTypeFullName;
			name.stringValue = slot.name;
			property.ClearArray();
			property.arraySize = slot.property.Length;
			for (int i = 0; i < slot.property.Length; i++)
			{
				property.GetArrayElementAtIndex(i).stringValue = slot.property[i];
			}
			description.stringValue = slot.description;
			icon.objectReferenceValue = slot.icon;
			startFreq.intValue = slot.startFreq;
			endFreq.intValue = slot.endFreq;
			rangeMin.floatValue = slot.rangeMin;
			rangeMax.floatValue = slot.rangeMax;
			minValue.vector4Value = new Vector4(slot.rangeMin, 0, 0, 0);
			maxValue.vector4Value = new Vector4(slot.rangeMax, 0, 0, 0);
			channelMask.intValue = slot.channelMask;
			loops.intValue = slot.loops;

			Normalize normalize = ScriptableObject.CreateInstance<Normalize>();
			Remap remap = ScriptableObject.CreateInstance<Remap>();
			Damping damping = ScriptableObject.CreateInstance<Damping>();
			normalize.name = "Normalize";
			remap.name = "Remap";
			damping.name = "Damping";
			modificators.ClearArray();
			modificators.arraySize = 3;
			modificators.GetArrayElementAtIndex(0).objectReferenceValue = normalize;
			modificators.GetArrayElementAtIndex(1).objectReferenceValue = remap;
			modificators.GetArrayElementAtIndex(2).objectReferenceValue = damping;

			DefaultSpectrumResolver spectrumResolver = ScriptableObject.CreateInstance<DefaultSpectrumResolver>();
			spectrumResolver.bitAnimator = bitAnimator;
			spectrumResolver.startFreq = 0;
			spectrumResolver.endFreq = 150;
			resolver.objectReferenceValue = spectrumResolver;
			//  TODO: gradient not serialized 
			/*colors.objectReferenceValue = new Gradient(); 
			
			
			((BitAnimator)target).recordSlots [recordSlots.arraySize - 1].colors = new Gradient();*/
			InitReorderableLists();
			serializedObject.ApplyModifiedProperties();
		}
	}
	void OnAddProperty(BitAnimator.RecordSlot slot)
	{
		AddProperty(slot);
	}
	void RenderAddShaderProperties()
	{
		EditorGUILayout.Space();
		GUIContent text = new GUIContent("Add Property", "Add new (shader, particle system, blend shape, object transform) property");
		if(GUILayout.Button(text))
		{
			selectedSlotIndex = -1;
			PropertySearchWindow.SearchProperty(addPropertyButtonRect, availableVariables, OnAddProperty);
		}
		else if (Event.current.type == EventType.Repaint)
		{
			addPropertyButtonRect = GUILayoutUtility.GetLastRect();
			addPropertyButtonRect.position = EditorGUIUtility.GUIToScreenPoint(addPropertyButtonRect.position);
		}
		//menu.ShowAsContext();
		EditorGUILayout.Space();
	}
	void OnChangedProperty(BitAnimator.RecordSlot newSlot)
	{
		SerializedProperty slot = recordSlots.GetArrayElementAtIndex(selectedSlotIndex);
		SerializedProperty property = slot.FindPropertyRelative("property");
		string selectedProperty = property.GetArrayElementAtIndex(0).stringValue;
		
		if(selectedProperty != newSlot.property[0])
		{
			SerializedProperty name = slot.FindPropertyRelative("name");
			SerializedProperty type = slot.FindPropertyRelative("type");
			SerializedProperty typeSet = slot.FindPropertyRelative("typeSet");
			SerializedProperty customTypeFullName = slot.FindPropertyRelative("customTypeFullName");
			SerializedProperty description = slot.FindPropertyRelative("description");
			SerializedProperty icon = slot.FindPropertyRelative("icon");
			SerializedProperty rangeMin = slot.FindPropertyRelative("rangeMin");
			SerializedProperty rangeMax = slot.FindPropertyRelative("rangeMax");

			type.intValue = (int)newSlot.type;
			typeSet.intValue = (int)newSlot.typeSet;
			customTypeFullName.stringValue = newSlot.customTypeFullName;
			property.ClearArray();
			property.arraySize = newSlot.property.Length;
			for(int i = 0; i < property.arraySize; i++)
			{
				property.GetArrayElementAtIndex(i).stringValue = newSlot.property[i];
			}
			name.stringValue = newSlot.name;
			description.stringValue = newSlot.description;
			icon.objectReferenceValue = newSlot.icon;
			rangeMin.floatValue = newSlot.rangeMin;
			rangeMax.floatValue = newSlot.rangeMax;
			serializedObject.ApplyModifiedProperties();
		}
	}
	void RenderShaderProperties()
	{
		if(expanded.Length != recordSlots.arraySize)
		{
			bool[] newVisible = new bool[recordSlots.arraySize];
			for (int i = 0; i < expanded.Length && i < newVisible.Length; i++)
				newVisible[i] = expanded[i];
			for (int i = expanded.Length; i < newVisible.Length; i++)
				newVisible[i] = false;
			expanded = newVisible;
		}
		EditorGUILayout.Space();
		Color oldBackground = GUI.backgroundColor;
		GUIStyle style = new GUIStyle(EditorStyles.helpBox);
		using (var slotProperties = new EditorGUI.ChangeCheckScope())
		{
			for (int i = 0; i < recordSlots.arraySize; ++i)
			{
				//Выделяем цветом выделеный проперти
				if (selectedSlot == bitAnimator.recordSlots[i] && BitAnimatorWindow.target == bitAnimator)
					GUI.backgroundColor = Color.Lerp(GUI.backgroundColor, Color.blue + 0.5f * Color.green, 0.3f);
				else
					GUI.backgroundColor = oldBackground;

				using (var slotArea = new EditorGUILayout.VerticalScope(style))
				{
					SerializedProperty serializedProp;
					using (var headerArea = new EditorGUILayout.HorizontalScope(GUILayout.Height(16)))
					{
						Rect rect = headerArea.rect;
						EditorGUILayout.Space();
						float baseWidth = (rect.width - 40.0f) / 4.0f;

						Rect foldoutRect = new Rect(rect.x + 15.0f, rect.y - 1.0f, 20.0f, rect.height);
						Rect popupRect = new Rect(rect.x + 20.0f, rect.y - 1.0f, baseWidth * 3.0f, rect.height);
						Rect removeRect = new Rect(rect.x + 35.0f + baseWidth * 3.0f, rect.y, baseWidth, rect.height);

						bool wasCollapsed = !expanded[i];
						expanded[i] = EditorGUI.Foldout(foldoutRect, expanded[i], GUIContent.none);
						EditorGUILayout.Space();

						serializedProp = recordSlots.GetArrayElementAtIndex(i);
						SerializedProperty property = serializedProp.FindPropertyRelative("property");
						SerializedProperty description = serializedProp.FindPropertyRelative("description");
						SerializedProperty icon = serializedProp.FindPropertyRelative("icon");
						SerializedProperty name = serializedProp.FindPropertyRelative("name");
						//SerializedProperty typeSet = serializedProp.FindPropertyRelative("typeSet");
						string mainProperty = property.GetArrayElementAtIndex(0).stringValue;

						//Выделяем проперти который был только что развернут
						if (expanded[i] && wasCollapsed)
						{
							BitAnimatorWindow.target = bitAnimator;
							selectedSlot = bitAnimator.recordSlots[i];
						}
						GUIContent label = new GUIContent(name.stringValue, (Texture)icon.objectReferenceValue, description.stringValue);
						if (GUI.Button(popupRect, label, EditorStyles.popup))
						{
							selectedSlotIndex = i;
							popupRect.position = EditorGUIUtility.GUIToScreenPoint(popupRect.position);
							PropertySearchWindow.SearchProperty(popupRect, availableVariables, OnChangedProperty);
						}
						if (GUI.Button(removeRect, "Remove"))
						{
							recordSlots.DeleteArrayElementAtIndex(i);
							InitReorderableLists();
							if (selectedSlot == bitAnimator.recordSlots[i])
								selectedSlot = null;
							serializedObject.ApplyModifiedProperties();
							continue;
						}
					}
					GUI.backgroundColor = oldBackground;

					if (!expanded[i])
					{
						continue;
					}
					using (new EditorGUI.IndentLevelScope(+1))
					{
						SerializedProperty type = serializedProp.FindPropertyRelative("type");
						SerializedProperty minValue = serializedProp.FindPropertyRelative("minValue");
						SerializedProperty maxValue = serializedProp.FindPropertyRelative("maxValue");
						//SerializedProperty startFreq = serializedProp.FindPropertyRelative("startFreq");
						//SerializedProperty endFreq = serializedProp.FindPropertyRelative("endFreq");
						//SerializedProperty rangeMin = serializedProp.FindPropertyRelative("rangeMin");
						//SerializedProperty rangeMax = serializedProp.FindPropertyRelative("rangeMax");
						SerializedProperty colors = serializedProp.FindPropertyRelative("colors");
						//SerializedProperty loops = serializedProp.FindPropertyRelative("loops");
						//SerializedProperty modificators = serializedProp.FindPropertyRelative("modificators");
						SerializedProperty resolver = serializedProp.FindPropertyRelative("resolver");
						SerializedProperty targetObjectProp = serializedProp.FindPropertyRelative("targetObject");

						EditorGUILayout.PropertyField(targetObjectProp);
						GameObject targetObject = targetObjectProp.objectReferenceValue as GameObject;
						if (targetObject != null && targetObject != animatorGO && !targetObject.transform.IsChildOf(animatorGO.transform))
						{
							EditorGUILayout.HelpBox("Target gameobject must be a child of animator object", MessageType.Warning);
						}
						switch ((BitAnimator.PropertyType)type.intValue)
						{
							case BitAnimator.PropertyType.Float:
								minValue.vector4Value = new Vector4(EditorGUILayout.FloatField("Min value", minValue.vector4Value.x), 0, 0, 0);
								maxValue.vector4Value = new Vector4(EditorGUILayout.FloatField("Max value", maxValue.vector4Value.x), 0, 0, 0);
								break;
							case BitAnimator.PropertyType.Range:
								minValue.vector4Value = new Vector4(EditorGUILayout.FloatField("Min value", minValue.vector4Value.x), 0, 0, 0);
								maxValue.vector4Value = new Vector4(EditorGUILayout.FloatField("Max value", maxValue.vector4Value.x), 0, 0, 0);
								break;
							case BitAnimator.PropertyType.Vector:
								minValue.vector4Value = EditorGUILayout.Vector4Field("Min value", minValue.vector4Value);
								maxValue.vector4Value = EditorGUILayout.Vector4Field("Max value", maxValue.vector4Value);
								break;
							case BitAnimator.PropertyType.Color:
								EditorGUI.BeginChangeCheck();
								EditorGUILayout.PropertyField(colors);
								if (EditorGUI.EndChangeCheck())
								{
									serializedObject.ApplyModifiedProperties();
									Gradient gradient = bitAnimator.recordSlots[i].colors;
									Vector4 min, max;
									min.x = gradient.colorKeys.Min(k => k.color.r);
									min.y = gradient.colorKeys.Min(k => k.color.g);
									min.z = gradient.colorKeys.Min(k => k.color.b);
									min.w = gradient.alphaKeys.Min(k => k.alpha);

									max.x = gradient.colorKeys.Max(k => k.color.r);
									max.y = gradient.colorKeys.Max(k => k.color.g);
									max.z = gradient.colorKeys.Max(k => k.color.b);
									max.w = gradient.alphaKeys.Max(k => k.alpha);
									minValue.vector4Value = min;
									maxValue.vector4Value = max;
								}
								break;
							case BitAnimator.PropertyType.TexEnv:
								EditorGUILayout.HelpBox("Textures haven't animation parameters", MessageType.Warning);
								break;
							case BitAnimator.PropertyType.Int:
								minValue.vector4Value = new Vector4(EditorGUILayout.IntField("Min value", Mathf.RoundToInt(minValue.vector4Value.x)), 0, 0, 0);
								maxValue.vector4Value = new Vector4(EditorGUILayout.IntField("Max value", Mathf.RoundToInt(maxValue.vector4Value.x)), 0, 0, 0);
								break;
							case BitAnimator.PropertyType.Vector3:
								minValue.vector4Value = EditorGUILayout.Vector3Field("Min value", minValue.vector4Value);
								maxValue.vector4Value = EditorGUILayout.Vector3Field("Max value", maxValue.vector4Value);
								break;
							case BitAnimator.PropertyType.Quaternion:
								//Значения задаются углами Эйлера. Потом эти углы будут конвертированы в кватернионы во время записи анимации
								minValue.vector4Value = EditorGUILayout.Vector3Field("Min rotation", minValue.vector4Value);
								maxValue.vector4Value = EditorGUILayout.Vector3Field("Max rotation", maxValue.vector4Value);
								break;
						}
						SpectrumResolver sr = (SpectrumResolver)resolver.objectReferenceValue;
						if (BitAnimator.interfaceMode >= BitAnimator.InterfaceMode.Expert)
						{
							int srIndex = Array.IndexOf(ResolverFactory.types, sr.GetType());
							int srIndex2 = EditorGUILayout.Popup("Resolver", srIndex, ResolverFactory.names);
							if(srIndex != srIndex2)
							{
								sr = ResolverFactory.CreateByIndex(srIndex2);
								sr.bitAnimator = bitAnimator;
								resolver.objectReferenceValue = sr;
							}
						}	
						sr.DrawProperty();

						if (BitAnimator.interfaceMode >= BitAnimator.InterfaceMode.Default)
						{
							GUILayout.Space(6);
							using (new EditorGUI.IndentLevelScope(-1))
							{
								modificatorLists[i].DoLayoutList();
							}
							EditorGUILayout.Space();
						}
					}
				}
			}
			if (slotProperties.changed)
			{
				updateAnimation = true;
				if (BitAnimatorWindow.instance != null)
				{
					serializedObject.ApplyModifiedProperties();
					BitAnimatorWindow.instance.ResetView();
				}
			}
		}
		//GUI.backgroundColor = oldBackground;
	}

	[Serializable]
	protected class DublicateException : Exception
	{
		public DublicateException()
		{
		}

		public DublicateException(string message) : base(message)
		{
		}

		public DublicateException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected DublicateException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}

[CustomPropertyDrawer(typeof(AudioVisualization.Plan))]
public class PlanDrawer : PropertyDrawer
{
	static readonly float[] qualityVariants = new float[] { 0.0f, 0.3f, 0.5f, 0.7f, 1.0f };
	static readonly GUIContent animationQualityLabel = new GUIContent("Animation quality", "0.0 - Maximum compression\n0.5 - normal quality\n1.0 - lossless");
	static readonly GUIContent sampleSizeLabel = new GUIContent("Sample size", "(FFT Window size) Higher values give more accuracy but also more input-lag");
	static readonly GUIContent multisamplesLabel = new GUIContent("Multisamples", "More samples - higher precision");
	static readonly GUIContent modesLabel = new GUIContent("Calculate modes", "EnergyCorrection - Normilize spectrum by wave frequency energy (E = frequency*amplitude)\nCalculateFons - Calculate volume adjusted for human perception");
	static readonly GUIContent filterLabel = new GUIContent("Filter", "Which a window function apply to audio to reduce noise");
	static readonly GUIContent filterParamLabel = new GUIContent("Filter parameter", "Side-lobe level in decibels");
	static readonly GUIContent[] options = new GUIContent[] { new GUIContent("Maximum compression"), new GUIContent("Balance"), new GUIContent("Normal"), new GUIContent("High quality"), new GUIContent("Lossless") };
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		SerializedProperty quality = property.FindPropertyRelative("quality");

		if(BitAnimator.interfaceMode >= BitAnimator.InterfaceMode.Expert)
		{
			EditorGUILayout.PropertyField(quality, animationQualityLabel);
		}
		else
		{
			float q = quality.floatValue;
			int idQuality;
			for(idQuality = qualityVariants.Length - 1; idQuality > 0; idQuality--)
				if(q >= qualityVariants[idQuality])
					break;
			EditorGUI.BeginChangeCheck();
			idQuality = EditorGUILayout.Popup(animationQualityLabel, idQuality, options);
			if(EditorGUI.EndChangeCheck())
				quality.floatValue = qualityVariants[idQuality];
		}
		SerializedProperty windowLogSize = property.FindPropertyRelative("windowLogSize");
		EditorGUILayout.PropertyField(windowLogSize, sampleSizeLabel);

		if(BitAnimator.interfaceMode >= BitAnimator.InterfaceMode.Default)
		{
			SerializedProperty multisamples = property.FindPropertyRelative("multisamples");
			EditorGUILayout.PropertyField(multisamples, multisamplesLabel);

			if(BitAnimator.interfaceMode >= BitAnimator.InterfaceMode.Expert)
			{
				SerializedProperty mode = property.FindPropertyRelative("mode");
				SerializedProperty filter = property.FindPropertyRelative("filter");
				SerializedProperty windowParam = property.FindPropertyRelative("windowParam");

				mode.intValue = (int)(Mode)EditorGUILayout.EnumFlagsField(modesLabel, (Mode)mode.intValue);
				EditorGUILayout.PropertyField(filter, filterLabel);

				if(DSPLib.DSP.Window.IsParametrizedWindow((DSPLib.DSP.Window.Type)filter.intValue))
					EditorGUILayout.PropertyField(windowParam, filterParamLabel);
			}
		}
	}
}