
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

Shader "Unlit/BitAnimatorRenderer/UI Text"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
		_Color("Color", Color) = (1, 1, 1, 1)
		_RenderTargetSize("RenderTargetSize", Vector) = (512, 256, 0.002, 0.004)
		_Mode("Tweaks", Int) = 1
    }
    SubShader
    {
        Pass
        {
			Blend SrcAlpha OneMinusSrcAlpha
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
			float4 _Color;
            float4 _RenderTargetSize;
			int _Mode;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag (v2f i) : COLOR
            {
				float4 color = tex2D(_MainTex, i.uv);
				//float2 dxy = abs(float2(ddx(color.a), ddy(color.a)));
				//float d = max(dxy.x, dxy.y);
				//color.a = saturate(color.a / d + 0.5);
				return color;
            }
            ENDCG
        }
    }
}
