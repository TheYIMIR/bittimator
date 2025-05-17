
// Copyright © 2019 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.2 (21.11.2019)

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AudioVisualization;
using AudioVisualization.Modificators;

[CustomEditor(typeof(SpectrumTexture))]
public class SpectrumTextureEditor : BitAnimatorEditor 
{
	SerializedProperty outputChanels;
	SerializedProperty spectrumTextureAsset;

	SpectrumTexture spectrumTexture = null;
	AudioClip audio = null;
	new protected void OnEnable()
	{
		base.OnEnable();
		outputChanels = serializedObject.FindProperty ("outputChanels");
		spectrumTextureAsset = serializedObject.FindProperty ("spectrumTextureAsset");
	}
	public override void OnInspectorGUI()
	{
		spectrumTexture = target as SpectrumTexture;
		SetupRecordSlot();
		serializedObject.Update();

		EditorGUILayout.PropertyField(audioClip, new GUIContent("Source Audioclip"));
		audio = audioClip.objectReferenceValue as AudioClip;
		if (audio == null)
		{
			serializedObject.ApplyModifiedProperties();
			return;
		}
		GUILayout.Label(String.Format("AudioClip duration = {0:F1} seconds", audio.length));
		EditorGUI.BeginChangeCheck();
		DrawInterfaceMode();
		SerializedProperty serializedProp = recordSlots.GetArrayElementAtIndex(0);
		SerializedProperty colors = serializedProp.FindPropertyRelative("colors");
		if (BitAnimator.interfaceMode >= BitAnimator.InterfaceMode.Default)
		{	
			EditorGUILayout.PropertyField(engineType);
			EditorGUILayout.PropertyField(plan);
			if (BitAnimator.interfaceMode >= BitAnimator.InterfaceMode.Expert)
			{
				SerializedProperty modificators = serializedProp.FindPropertyRelative("modificators");
				SerializedProperty resolver = serializedProp.FindPropertyRelative("resolver");

				SpectrumResolver sr = (SpectrumResolver)resolver.objectReferenceValue;
				if (sr != null)
					sr.DrawProperty();

				EditorGUILayout.PropertyField(outputChanels, new GUIContent("Frequency channels"));
				SerializedProperty mod = modificators.GetArrayElementAtIndex(1);
				Remap remap = (Remap)mod.objectReferenceValue;
				remap.remap = EditorGUILayout.CurveField("Remap by frequencies", remap.remap);
			}
		}
		EditorGUILayout.PropertyField(colors, new GUIContent("Colors"));
		EditorGUILayout.PropertyField(spectrumTextureAsset, new GUIContent("Output texture"));

		serializedObject.ApplyModifiedProperties();

		EditorGUILayout.Space();
		using (new EditorGUI.DisabledScope(!IsLoadableAudio()))
		{
			if (GUILayout.Button("Create Spectrum Texture", GUILayout.MaxWidth(200)))
			{
				if (spectrumTextureAsset.objectReferenceValue == null)
				{
					string path = EditorUtility.SaveFilePanelInProject("Save as...", audio.name + ".png", "png", "Save spectrum texture");
					serializedObject.ApplyModifiedProperties();
					spectrumTexture.CreateSpectrumTexture(path);
				}
				else
				{
					string path = AssetDatabase.GetAssetPath(spectrumTextureAsset.objectReferenceValue);
					spectrumTexture.CreateSpectrumTexture(path);
				}
			}
		}
	}
	void SetupRecordSlot()
	{
		if (spectrumTexture.recordSlots.Count >= 1)
			return;
		spectrumTexture.recordSlots.Add( new BitAnimator.RecordSlot
		{
			type = BitAnimator.PropertyType.Color,
			typeSet = BitAnimator.RecordSlot.PropertiesSet.Material,
			name = "color",
			description = "Spectrum texture",
			startFreq = 50,
			endFreq = 22050,
			rangeMin = 0,
			rangeMax = 1,
			minValue = new Vector4(0, 0, 0, 0),
			maxValue = new Vector4(1, 1, 1, 1),
			channelMask = 0xFF,
			loops = 1,
			multiply = 1,
			colors = GetFireGradient(),
			modificators = new List<Modificator>(2)
			{
				CreateInstance<Normalize>(),
				CreateInstance<Remap>()
			}
		});
		((Remap)spectrumTexture.recordSlots[0].modificators[1]).remap = AnimationCurve.Linear(0, 1.5f, 1.0f, 15.0f);
	}
	Gradient GetFireGradient()
	{
		Gradient gradient = new Gradient();

		GradientColorKey[] colorKey = new GradientColorKey[4];
		colorKey[0].color = Color.black;
		colorKey[0].time = 0.0f;
		colorKey[1].color = Color.red;
		colorKey[1].time = 0.33f;
		colorKey[2].color = Color.yellow;
		colorKey[2].time = 0.66f;
		colorKey[3].color = Color.white;
		colorKey[3].time = 1.0f;

		GradientAlphaKey[] alphaKey = new GradientAlphaKey[2];
		alphaKey[0].alpha = 1.0f;
		alphaKey[0].time = 0.0f;
		alphaKey[1].alpha = 1.0f;
		alphaKey[1].time = 1.0f;

		gradient.SetKeys(colorKey, alphaKey);
		return gradient;
	}
}