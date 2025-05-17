
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using AudioVisualization;

public class BitAnimatorWindow : EditorWindow
{
	public TextAsset serializedPresets;
	public RenderTexture texture;

	ISpectrumRenderer renderer;
	PlotGraphic plotGraphic = PlotGraphic.Peaks;
	Rect box = new Rect(0, 0, 1, 1);
	Vector2 pos, mouseDown, scroll;
	bool openSettings = true;
	bool applyMods = true;
	float speed = 1.0f;
	float scale = 3.0f;
	float time;
	int selectedBitAnimator = -1;

	public static BitAnimatorWindow instance;
	public static float executionTime;
	public static BitAnimator target;
	public static Animation animation;
	public static AudioSource audio;
	public static ParticleSystem particles;
	public static bool isRunningTask;
	public static bool resetInNextFrame;
	
	//static List<BitAnimatorEditor.EngineDescriptor> engines;
	public int targetID;
	Engine.CoreType engineType;
	bool wasPlaying;

	float AudioTime
	{
		get
		{
			return audio.time;
		}
		set
		{
			int newSamples = Mathf.RoundToInt(value * audio.clip.frequency);
			audio.timeSamples = Mathf.Clamp(newSamples, 0, audio.clip.samples - 1);
		}
	}

	static BitAnimatorWindow()
	{
		EditorApplication.playModeStateChanged += PlayModeStateChanged;
		//engines = BitAnimatorEditor.GetAllEngines(renderer: true);
	}
	void Awake()
	{
		wantsMouseMove = true;
	}
	void OnEnable()
	{
		if (!EditorApplication.isPlaying)
			EditorApplication.update += UpdateAnimation;
	}
	void OnDisable()
	{
		ResetState();
		if (!EditorApplication.isPlaying)
			EditorApplication.update -= UpdateAnimation;
	}
	void OnGUI()
	{
		if (target == null)
		{
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			bool createNew = GUILayout.Button("Create new BitAnimator", GUILayout.MinHeight(40), GUILayout.MaxWidth(180));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			if (createNew)
			{
				GameObject animatorGO = new GameObject("New Animator");
				target = animatorGO.AddComponent<BitAnimator>();
				target.animatorObject = animatorGO.AddComponent<Animator>();
				AudioSource audioSource = animatorGO.AddComponent<AudioSource>();
				audioSource.minDistance = 10;
				audioSource.volume = 0.25f;
				audioSource.dopplerLevel = 0;
				audioSource.spatialize = false;
				GameObject targetGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
				targetGO.transform.parent = animatorGO.transform;
				DestroyImmediate(targetGO.GetComponent<BoxCollider>());
				target.targetObject = targetGO;
				Selection.activeObject = target;
			}
			else
			{
				BitAnimator[] bitAnimators = Resources.FindObjectsOfTypeAll<BitAnimator>();
				if (bitAnimators.Length > 0)
				{
					GUILayout.FlexibleSpace();
					target = (BitAnimator)EditorGUILayout.ObjectField("BitAnimator", target, typeof(BitAnimator), true);
					GUILayout.Label("Or select exists animator:");
					scroll = GUILayout.BeginScrollView(scroll, EditorStyles.helpBox);
					selectedBitAnimator = GUILayout.SelectionGrid(selectedBitAnimator, bitAnimators.Select(c =>
					{
						return c.gameObject.name + (c.audioClip != null ? " | " + c.audioClip.name : "");
					}).ToArray(), 1, EditorStyles.radioButton);
					GUILayout.EndScrollView();
					if (selectedBitAnimator >= 0)
					{
						Selection.activeObject = bitAnimators[selectedBitAnimator];
						GUILayout.BeginHorizontal();
						GUILayout.FlexibleSpace();
						if (GUILayout.Button("Open", GUILayout.MinHeight(40), GUILayout.MaxWidth(180)))
						{
							target = bitAnimators[selectedBitAnimator];
							selectedBitAnimator = -1;
						}
						GUILayout.FlexibleSpace();
						GUILayout.EndHorizontal();
					}
				}
				GUILayout.FlexibleSpace();
			}
			GUILayout.EndVertical();
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}
		else if(target.recordSlots.Count == 0)
		{
			target = (BitAnimator)EditorGUILayout.ObjectField("BitAnimator", target, typeof(BitAnimator), true);
			EditorGUILayout.HelpBox("This window only for visualization. Setup BitAnimator in the inspector window", MessageType.Info);
		}
		else
		{
			openSettings = EditorGUILayout.Foldout(openSettings, "Show settings", true);
			if (openSettings)
			{
				++EditorGUI.indentLevel;
				target = (BitAnimator)EditorGUILayout.ObjectField("BitAnimator", target, typeof(BitAnimator), true);
				{
					EditorGUI.BeginChangeCheck();
					engineType = (Engine.CoreType)EditorGUILayout.EnumPopup("Engine", engineType);
					if (EditorGUI.EndChangeCheck())
					{
						InitializeEngine();
					}
				}
				using (new EditorGUI.DisabledScope(audio == null || animation == null))
				{
					if (audio != null && audio.clip != null)
					{
						EditorGUI.BeginChangeCheck();
						time = audio.time;
						GUILayout.BeginHorizontal();
						speed = EditorGUILayout.Slider("Playback speed", speed, -2, 2);
						if (GUILayout.Button("Reset"))
							speed = 1.0f;
						GUILayout.EndHorizontal();
						time = EditorGUILayout.Slider("Animation play time", time, 0, audio.clip.length);
						bool changes = EditorGUI.EndChangeCheck();
						if (animation != null)
						{
							AnimationState anim = animation["BitAnimator.RuntimeAnimation"];
							if (changes)
							{
								Time.timeScale = Mathf.Abs(audio.pitch = speed);
								bool oldState = anim.enabled;
								anim.enabled = true;
								AudioTime = time;
								anim.time = time;
								animation.Sample();
								anim.enabled = oldState;
								if (particles != null)
									particles.time = audio.time;
							}
							else if (Mathf.Abs(anim.time - audio.time) > 0.05f)
							{
								anim.time = audio.time;
								if (particles != null)
									particles.time = audio.time;
							}
						}
					}
					else
					{
						time = EditorGUILayout.Slider("Animation play time", time, 0, 0);
						speed = EditorGUILayout.Slider("Playback speed", speed, -2, 2);
					}
					
					GUILayout.BeginHorizontal();

					EditorGUI.BeginChangeCheck();
					float beat = (time - target.beatOffset / 1000.0f) * target.bpm / 60.0f;
					beat = EditorGUILayout.FloatField("Beat", beat);
					if(EditorGUI.EndChangeCheck())
					{
						time = Mathf.Clamp(beat * 60.0f / target.bpm + target.beatOffset / 1000.0f, 0, audio.clip.length);
						if(audio != null && audio.clip != null)
							AudioTime = time;
						if(animation != null)
							animation["BitAnimator.RuntimeAnimation"].time = time;
						if(particles != null)
							particles.time = time;
					}
					EditorGUI.BeginChangeCheck();
					if(renderer != null)
						applyMods = EditorGUILayout.ToggleLeft("Apply mods", applyMods);
					else
						EditorGUILayout.ToggleLeft("Apply mods", false);
					if(EditorGUI.EndChangeCheck())
						ResetView();

					GUILayout.FlexibleSpace();
					if (audio != null && animation != null)
					{
						if (audio.isPlaying)
						{
							if (GUILayout.Button("Pause", GUILayout.MaxWidth(128)))
								Pause();
						}
						else
						{
							if (GUILayout.Button("Play", GUILayout.MaxWidth(128)))
								Play();
						}
					}
					else
					{
						GUILayout.Button("Play", GUILayout.MaxWidth(128));
					}
					GUILayout.EndHorizontal();
				}
				if (renderer != null)
				{
					EditorGUI.BeginChangeCheck();
					plotGraphic = (PlotGraphic)EditorGUILayout.EnumPopup("Plot graphic", plotGraphic);
					if (EditorGUI.EndChangeCheck())
					{
						if (texture == null)
						{
							texture = new RenderTexture(512, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
							texture.enableRandomWrite = true;
							texture.Create();
						}
						ResetView();
					}
					if (texture != null)
					{
						if (Event.current.type == EventType.Repaint)
						{
							box = GUILayoutUtility.GetRect(position.width, position.height);
							int w = Mathf.FloorToInt(box.width / 8.0f) * 8;
							int h = Mathf.FloorToInt(box.height / 8.0f) * 8;
							box.width = w;
							box.height = h;
							GUI.DrawTexture(box, texture);

							if (texture.width != w || texture.height != h)
							{
								texture.Release();
								texture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
								texture.enableRandomWrite = true;
								texture.Create();
							}
						}
						else
							GUILayout.Box(texture, GUILayout.MinWidth(32), GUILayout.MinHeight(32), GUILayout.MaxWidth(2048), GUILayout.MaxHeight(2048));
						float progress = (renderer as Engine).Progress;
						GUILayout.Label(String.Format("Loading... {0}%", Mathf.RoundToInt(progress * 100.0f)));
					}
				}
				--EditorGUI.indentLevel;
			}
			else if(texture != null)
			{
				if (Event.current.type == EventType.Repaint)
				{
					Rect rect = GUILayoutUtility.GetRect(position.width, position.height);
					GUI.DrawTexture(rect, texture);

					int w = Mathf.FloorToInt(rect.width / 8.0f) * 8;
					int h = Mathf.FloorToInt(rect.height / 8.0f) * 8;
					if (texture.width != w || texture.height != h)
					{
						texture.Release();
						texture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
						texture.enableRandomWrite = true;
						texture.Create();
					}
				}
				else
					GUILayout.Box(texture, GUILayout.MinWidth(32), GUILayout.MinHeight(32), GUILayout.MaxWidth(2048), GUILayout.MaxHeight(2048));
			}
			EditorGUIUtility.AddCursorRect(box, MouseCursor.SlideArrow);

			if (Event.current.type == EventType.MouseDown)
			{
				mouseDown = Event.current.mousePosition;
				if(audio != null && audio.isPlaying && box.Contains(mouseDown))
				{
					wasPlaying = true;
					Pause();
				}
			}
			else if (Event.current.type == EventType.MouseUp)
			{
				mouseDown = -Vector2.one;
				if (wasPlaying)
				{
					wasPlaying = false;
					Play();
				}
			}
			else if (Event.current.type == EventType.MouseDrag && box.Contains(mouseDown))
			{
				if (mouseDown.x >= 0 && audio != null && audio.clip != null)
				{
					AudioTime = audio.time - Event.current.delta.x / box.width * scale;
					if (animation != null)
					{
						AnimationState anim = animation["BitAnimator.RuntimeAnimation"];
						anim.enabled = true;
						anim.time = audio.time;
						animation.Sample();
					}
					if(particles != null)
					{
						particles.time = audio.time;
					}
					Event.current.Use();
				}
			}
			else if (Event.current.type == EventType.MouseMove)
			{
				pos = Event.current.mousePosition;
				if (box.Contains(pos))
					pos -= box.position;
				else
					pos = Vector2.zero;
			}
			else if (Event.current.type == EventType.ScrollWheel)
			{
				if (box.Contains(Event.current.mousePosition))
				{
					//scale *= Event.current.delta.y < 0 ? 1.0f / 1.125f : 1.125f;
					scale *= Mathf.Exp(Event.current.delta.y * 0.1f);
					scale = Mathf.Clamp(scale, 0.1f, audio.clip.length * 2);
					renderer.ViewScale = scale;
					Event.current.Use();
				}
			}
		}
	}
	void Pause()
	{
		audio.Pause();
		animation.Stop();
		Time.timeScale = 0.0f;
	}
	void Play()
	{
		audio.Play();
		animation.Play();
		animation["BitAnimator.RuntimeAnimation"].enabled = true;
		animation["BitAnimator.RuntimeAnimation"].time = audio.time;
		if (particles != null)
			particles.time = audio.time;
		Time.timeScale = Mathf.Abs(speed);
	}
	void Update()
	{ 
		if (!isRunningTask)
		{
			if (!EditorApplication.isPaused && renderer != null && target != null && target.recordSlots.Count > 0 && animation != null && audio != null)
			{
				animation["BitAnimator.RuntimeAnimation"].enabled = true;
				if (texture == null)
				{
					texture = new RenderTexture(512, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
					texture.enableRandomWrite = true;
					texture.Create();
				}

				//float mul = autoMultiply * multiply;
				float mul = 1.0f;
				renderer.Render(texture, audio.time, mul);

				/*if(renderer.Maximum != 0.0f)
				{
					maximum = renderer.Maximum;
					if(resetInNextFrame)
					{
						if(float.IsInfinity(multiply) || float.IsNaN(multiply))
							multiply = 1.0f;
						if(float.IsInfinity(autoMultiply) || float.IsNaN(autoMultiply))
							autoMultiply = 1.0f;
						if(float.IsInfinity(maximum) || float.IsNaN(maximum))
							maximum = 1.0f;
						autoMultiply = 0.95f / (maximum / multiply);
						resetInNextFrame = false;
					}
					//if (audio.isPlaying && (mode & Mode.RuntimeNormalize) != 0)
					{
						float delta = 0.95f / (maximum / multiply) - autoMultiply;
						if(delta < 0)
						{
							float k = 1.0f - Mathf.Exp(-Time.deltaTime * autoNormilizeSpeed);
							autoMultiply += k * delta;
						}
					}
				}*/
				Repaint();
			}
		}
	}
	public ISpectrumRenderer CreateEngine(Engine.CoreType type)
	{
		switch (type)
		{
			case Engine.CoreType.Auto: return new LegacySpectrumRenderer();
			case Engine.CoreType.Legacy: return new LegacySpectrumRenderer();
			case Engine.CoreType.ComputeShaders: return new CSSpectrumRenderer();
			default: return null;
		}
	}
	public void SelectEngine(Engine.CoreType type)
	{
		if (ReferenceEquals(renderer, null))
		{
			renderer = CreateEngine(type);
		}
		else if (type == Engine.CoreType.Auto)
		{
			return;
		}
		else if (renderer.Type != type)
		{
			renderer.Dispose();
			renderer = CreateEngine(type);
		}
	}
	public void InitializeEngine()
	{
		SelectEngine(engineType);
		Engine engine = renderer as Engine;
		if (engine != null && target != null)
		{
			engine.beatOffset = target.beatOffset;
			engine.bpm = target.bpm;
		}
		renderer.Initialize(target.engineSettings[(int)target.engineType], target.plan, target.audioClip);
		ResetView();
	}
	public void ResetView(bool resetMultimpier = true)
	{
		if (renderer != null)
		{
			if (BitAnimatorEditor.selectedSlot == null)
				BitAnimatorEditor.selectedSlot = target.recordSlots[0];

			renderer.ApplyMods = applyMods;
			renderer.ViewScale = scale;
			renderer.SetTask(BitAnimatorEditor.selectedSlot, plotGraphic);
		}
		resetInNextFrame = resetMultimpier;
	}

	[MenuItem("Window/BitAnimator")]
	static void Init()
	{
		GetOrAddWindow().Show();
	}
	public static BitAnimatorWindow GetOrAddWindow()
	{
		if (instance == null)
			instance = EditorWindow.GetWindow<BitAnimatorWindow>("BitAnimator");
		return instance;
	}
	public static void ResetState()
	{
		Time.timeScale = 1.0f;
		if (animation != null)
		{
			AnimationState runtimeAnimation = animation["BitAnimator.RuntimeAnimation"];
			if (runtimeAnimation != null)
			{
				runtimeAnimation.enabled = true;
				runtimeAnimation.normalizedTime = 1.0f;
				animation.Sample();
				runtimeAnimation.enabled = false;
				animation.Stop();
			}
			DestroyImmediate(animation);
		}
		if (audio != null)
		{
			audio.pitch = 1.0f;
			audio.Stop();
		}
		if (instance != null && instance.renderer != null)
		{
			instance.renderer.Dispose();
			instance.renderer = null;
		}
		EditorCoroutines.StopAll();
		isRunningTask = false;
	}
	static void UpdateAnimation()
	{
		if (!isRunningTask)
		{
			if (animation != null)
			{
				animation["BitAnimator.RuntimeAnimation"].time = audio.time;
				animation.Sample();
			}
			if (particles != null)
			{
				particles.time = audio.time;
			}
		}
	}
	static void PlayModeStateChanged(PlayModeStateChange state)
	{
		switch (state)
		{
			case PlayModeStateChange.EnteredEditMode:
				isRunningTask = false;
				EditorUtility.ClearProgressBar();
				break;

			case PlayModeStateChange.ExitingEditMode:
				break;

			case PlayModeStateChange.EnteredPlayMode:
				GetOrAddWindow();
				if (instance.targetID == 0)
				{
					instance.Close();
					return;
				}
				
				target = EditorUtility.InstanceIDToObject(instance.targetID) as BitAnimator;
				instance.targetID = 0;
				if (target != null)
					SetupRuntimeAnimation(target);

				break;

			case PlayModeStateChange.ExitingPlayMode:
				break;
		}
	}
	public static void SetupRuntimeAnimation(BitAnimator bitAnimator)
	{
		Animator animator = bitAnimator.animatorObject;
		animation = animator.gameObject.GetComponent<Animation>();
		if(animation == null)
			animation = animator.gameObject.AddComponent<Animation>();
		animation.playAutomatically = false;
		AnimationState runtimeAnimation = animation["BitAnimator.RuntimeAnimation"];
		if (runtimeAnimation == null)
		{
			AnimationClip clip = new AnimationClip
			{
				name = bitAnimator.audioClip.name,
				legacy = true
			};
			animation.AddClip(clip, "BitAnimator.RuntimeAnimation");
			runtimeAnimation = animation["BitAnimator.RuntimeAnimation"];
		}
		animation.clip = runtimeAnimation.clip;
		animation.clip.frameRate = (float)bitAnimator.audioClip.frequency / bitAnimator.plan.WindowSize * bitAnimator.plan.multisamples;
		Action<BitAnimator> finishCallback = (BitAnimator b) =>
		{
			isRunningTask = false;
			executionTime = Time.realtimeSinceStartup - executionTime;
			//EditorUtility.ClearProgressBar();
			animation.Play();
			audio.loop = bitAnimator.loop;
			audio.enabled = true;
			if (!audio.isPlaying)
				audio.Play();
			if (particles != null && (!particles.isPlaying || particles.isPaused))
				particles.Play(true);
			animation["BitAnimator.RuntimeAnimation"].enabled = true;
		};
		EditorCoroutines.Start(bitAnimator.ComputeAnimation(runtimeAnimation.clip, finishCallback));
		if (EditorApplication.isPlaying)
			bitAnimator.animatorObject.enabled = false;
		isRunningTask = true;
		//EditorCoroutines.StartCoroutine(ProgressBar(bitAnimator));
		executionTime = Time.realtimeSinceStartup;
		
		audio = bitAnimator.animatorObject.gameObject.GetComponentInChildren<AudioSource>();
		if (audio == null)
			audio = bitAnimator.animatorObject.gameObject.AddComponent<AudioSource>();

		if (audio.clip != bitAnimator.audioClip)
		{
			audio.clip = bitAnimator.audioClip;
			audio.volume = 0.25f;
			audio.minDistance = 10;
			audio.dopplerLevel = 0;
			audio.spatialBlend = 0;
			audio.spatialize = false;
			audio.timeSamples = 0;
		}
		particles = bitAnimator.targetObject.GetComponent<ParticleSystem>();

		target = bitAnimator;
		GetOrAddWindow().InitializeEngine();
		Time.timeScale = instance.speed = 1.0f;
		//BitAnimatorWindowDebug.Instance.update = GetOrAddWindow().SyncUpdate;
	}
	public static void ClearRunTimeClip()
	{
		animation.clip.ClearCurves();
	}
	public static void WriteAnimation(BitAnimator bitAnimator)
	{
		Action<BitAnimator> finishCallback = (BitAnimator b) =>
		{
			isRunningTask = false;
			executionTime = Time.realtimeSinceStartup - executionTime;
			//EditorUtility.ClearProgressBar();
		};
		EditorCoroutines.Start(bitAnimator.CreateAnimation(finishCallback));
		target = bitAnimator;
		isRunningTask = true;
		//EditorCoroutines.StartCoroutine(ProgressBar(bitAnimator));
		executionTime = Time.realtimeSinceStartup;
	}
	static IEnumerator ProgressBar(BitAnimator bitAnimator)
	{
		while (isRunningTask)
		{
			if(bitAnimator.engine != null)
				EditorUtility.DisplayProgressBar("Creating animation", bitAnimator.engine.Status, bitAnimator.engine.Progress);
			yield return null;
		}
	}
	
}