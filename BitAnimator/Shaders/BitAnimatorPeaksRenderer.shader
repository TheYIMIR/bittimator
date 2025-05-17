
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

Shader "Unlit/BitAnimatorRenderer/Peaks"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
		_Offset("Offset", Float) = 0.0
		_Batches("Batches", Float) = 256.0
		_Multiply("Multiply", Float) = 1.0
		_AudioClipTime("AudioClip Time", Float) = 0.0
		_TimeStart("Start time of chunk", Float) = 0.0
		_TimeEnd("End time of chunk", Float) = 1.0
		_TimeScale("TimeScale", Float) = 3.0
		_BPM("BPM", Float) = 60.0
		_BeatOffset("Beat offset", Float) = 0.0
		_ChunksPerSecond("ChunksPerSecond", Float) = 1.0
		_HalfWindowTime("HalfWindowTime", Float) = 0.5
		_RenderTargetSize("RenderTargetSize", Vector) = (512, 256, 0.002, 0.004)
		_Mode("Tweaks", Int) = 1
    }
	CGINCLUDE
	#include "UnityCG.cginc"

	uniform sampler2D _MainTex;
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
	uniform float _BPM;
	uniform float _BeatOffset;
	uniform float _ChunksPerSecond;
	uniform float _HalfWindowTime;
	uniform int _Mode;

	struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	struct v2f
	{
		float4 vertex : SV_POSITION;
		float uv : TEXCOORD0;
		float chunk : TEXCOORD1;
		float apmlitude : TEXCOORD2;
		float beat : TEXCOORD3;
		float beat_delta : TEXCOORD4;
		float beat_line_color : TEXCOORD5;
	};

	void vert(in appdata v, out v2f o)
	{
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = TRANSFORM_TEX(v.uv, _MainTex).x;
		o.apmlitude = v.uv.y * 2 - 1;

		float startTime = _AudioClipTime - _TimeScale / 2.0;
		o.chunk = (startTime + v.uv.x * _TimeScale - _TimeStart) / (_TimeEnd - _TimeStart);

		float beatsPerSec = _BPM / 60.0;
		o.beat = (startTime + v.uv.x * _TimeScale - _BeatOffset) * beatsPerSec;
		float beatsPerPixel = _RenderTargetSize.z * _TimeScale * beatsPerSec;
		o.beat_delta = o.beat + beatsPerPixel;
		o.beat_line_color = saturate(0.01 / beatsPerPixel);
	}

	float smoothBands(float x, float width)
	{
		return saturate((abs(frac(x) - 0.5) - 0.5 + 0.5 * width) / ddx(x) + width);
	}

	float4 frag(in v2f i) : COLOR
	{
		const uint LogFrequency = 1;
		const uint LogAmplitude = 2;
		const uint EnergyCorrection = 4;
		const uint CalculateFons = 8;
		const uint SpectrumEnegry = 2048;

		if (i.chunk.x < 0 || 1 < i.chunk.x)
			discard;

		float bpmLine = floor(i.beat_delta) - floor(i.beat);
		float volume = tex2D(_MainTex, i.chunk.xx).r;
		volume *= _Multiply;
		float3 color = 0;

		color += bpmLine * i.beat_line_color * 0.25;

		if (abs(volume) > abs(i.apmlitude))
			color = float3(0, 0, 1);

		if (volume > abs(i.apmlitude))
		{
			if (_Mode & SpectrumEnegry)
			{
				color = saturate(float3(2, 2, 0) - abs(volume * float3(2, 2, 0) - float3(2, 0, 0)));
			}
			else
			{
				color = 1.0;
			}
		}

		if ((int)i.vertex.x * 2 == (int)_RenderTargetSize.x)
			color = 1.0 - color;

		return float4(color, 1.0);
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
