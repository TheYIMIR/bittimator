
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

Shader "Unlit/BitAnimatorRenderer/Histogram"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "black" {}
		_MaskTex ("Mask", 2D) = "black" {}
		_Multiply("Multiply", Float) = 1.0
		//_LowFrequency("LowFrequency", Float) = 0
		//_HighFrequency("HighFrequency", Float) = 100
		_Frequency("Frequency", Float) = 44100
		_FFTWindow("FFTWindow", Float) = 1024
		_Mode("Tweaks", Int) = 1
	}
	SubShader
	{
		Pass
		{
			ZWrite Off
			ZTest Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

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

			uniform sampler2D _MainTex;
			uniform sampler2D _MaskTex;
			uniform float4 _MainTex_ST;
			uniform float4 _MainTex_TexelSize;
			uniform float _Multiply;
			//uniform float _LowFrequency;
			//uniform float _HighFrequency;
			uniform float _Frequency;
			uniform float _FFTWindow;
			uniform int _Mode;

			void vert (appdata v, out v2f o)
			{
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
			}

			float4 frag(v2f i) : COLOR
			{
				const uint LogFrequency = 1;
				const uint LogAmplitude = 2;
				const uint EnergyCorrection = 4;
				const uint CalculateFons = 8;

				if (_Mode & LogFrequency)
					i.uv.x *= exp2(6.9 * (i.uv.x - 1.0));

				float volume = tex2Dbias(_MainTex, float4(i.uv, 0, 0)).r;
				
				if (_Mode & LogAmplitude)
				{
					//_Multiply = 5;
					//float A = exp2(-_Multiply);
					//volume = (log2(volume + A) + _Multiply) / (log2(1.0 + A) + _Multiply);

					volume = (1.0 - exp2(-28.0 * volume)) / (1.0 - exp2(-28.0));
					//volume = (log10(volume) + _Multiply) / _Multiply;
				}
				else
					volume *= _Multiply;

				float4 color = float4(0, 0, 0, 1);
				if (volume > i.uv.y)
					color = float4(0, 0.7, 0, 1);

				/*bool leftBorder = i.uv.x > _LowFrequency / _Frequency * 2;
				bool rightBorder = i.uv.x < _HighFrequency / _Frequency * 2;
				if (leftBorder && rightBorder)
					mask = float4(1, 0, 0, 1);*/
				float4 mask = tex2Dbias(_MaskTex, float4(i.uv, 0, 0));
				color = lerp(color, mask, mask.a);
				return color;
			}
			ENDCG
		}
	}
}
