
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

Shader "Unlit/BitAnimatorRenderer/Spectrum"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_MaskTex("Mask", 2D) = "black" {}
		_Offset("Offset", Float) = 0.0
		_Batches("Batches", Float) = 256.0
		_Multiply("Multiply", Float) = 1.0
		_TimeScale("TimeScale", Float) = 3.0
		_Frequency("Frequency", Float) = 44100
		_AudioClipTime("AudioClip Time", Float) = 0.0
		_TimeStart("Start time of chunk", Float) = 0.0
		_TimeEnd("End time of chunk", Float) = 1.0
		_ChunksPerSecond("ChunksPerSecond", Float) = 1.0
		_HalfWindowTime("HalfWindowTime", Float) = 0.5
		_RenderTargetSize("RenderTargetSize", Vector) = (512, 256, 0.002, 0.004)
		_Mode("Tweaks", Int) = 1
	}
	CGINCLUDE
	#include "UnityCG.cginc"

	uniform sampler2D _MainTex;
	uniform sampler2D _MaskTex;
	uniform float4 _MainTex_ST;
	uniform float4 _MainTex_TexelSize;
	uniform float4 _RenderTargetSize;
	uniform float _Offset;
	uniform float _Batches;
	uniform float _Multiply;
	uniform float _AudioClipTime;
	uniform float _TimeStart;
	uniform float _TimeEnd;
	uniform float _TimeScale;
	uniform float _ChunksPerSecond;
	uniform float _HalfWindowTime;
	uniform float _Frequency;
	uniform int _Mode;

	struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	struct v2f
	{
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
	};

	void vert(in appdata v, out v2f o)
	{
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = TRANSFORM_TEX(v.uv, _MainTex);
		float startTime = _AudioClipTime - _TimeScale / 2.0;
		o.uv.x = (startTime + v.uv.x * _TimeScale - _TimeStart) / (_TimeEnd - _TimeStart);
	}

	float4 frag(v2f i) : COLOR
	{
		const uint LogFrequency = 1;
		const uint LogAmplitude = 2;
		const uint EnergyCorrection = 4;
		const uint CalculateFons = 8;
		const uint SpectrumEnegry = 2048;

		if (i.uv.x < 0 || 1 < i.uv.x)
			discard;

		if (_Mode & LogFrequency)
			i.uv.y *= exp2(6.9 * (i.uv.y - 1.0));

		float volume = tex2D(_MainTex, i.uv.yx).r;
		if (_Mode & LogAmplitude)
		{
			volume = (1.0 - exp2(-28.0 * volume)) / (1.0 - exp2(-28.0));
		}
		else
		{
			volume = saturate(volume * _Multiply * (_Mode & EnergyCorrection ? 10 : 2));
		}

		float3 color = 0;
		if (_Mode & SpectrumEnegry)
		{
			color = saturate(2 - abs(volume * 6 - float3(6, 4, 2)));
		}
		else
		{
			color = float3(0, volume, 0);
		}
		float4 mask = tex2D(_MaskTex, i.uv.yx);
		color = lerp(color, mask, mask.a);

		if ((int)i.vertex.x * 2 == (int)_RenderTargetSize.x)
			color = 1.0 - color;

		return float4(color, 1);
	}
	ENDCG
	SubShader
	{
		Pass
		{
			ZWrite Off
			ZTest Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
	}
}
