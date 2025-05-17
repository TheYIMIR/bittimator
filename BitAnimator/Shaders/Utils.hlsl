
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

#define UNITY_PI 3.14159265358979323844f
#define M_E 2.71828182845904523536f

inline float2 conjunction(float2 c)
{
	return float2(c.x, -c.y);
}
inline float2 complexMultiply(float2 left, float2 right)
{
	return float2(dot(left, conjunction(right)), dot(left, right.yx));
}
inline float2 complexDivide(float2 c1, float2 c2)
{
	float l = dot(c2, c2);
	float real = dot(c1.xy, c2) / l;
	float imag = dot(c1.yx, c2) / l;
	return float2(real, imag);
}
inline float complexAbs(float2 c)
{
	return length(c);
}
inline float2 complexPolar(float2 c)
{
	return float2(complexAbs(c), atan2(c.y, c.x));
}
inline float2 complexRect(float2 c)
{
	return abs(c.x) * float2(cos(c.y), sin(c.y));
}
inline float2 complex_power(float2 base, float2 e)
{
	float2 b = complexPolar(base);
	float z = pow(b.x, e.x) * exp(-e.y * b.y);
	float fi = e.y * log(b.x) + e.x * b.y;
	float2 rpol = float2(z, fi);
	return complexRect(rpol);
}

float getFons(float hz, float Lp)
{
	//Acoustics — Normal equal-loudness-level contours
	//http://libnorm.ru/Files2/1/4293820/4293820821.pdf

	//  Hz, Alpha_f, Lu, Tf
	const float3 isofons[] = {
		float3(0.532f, -31.6f, 78.5f),
		float3(0.506f, -27.2f, 68.7f),
		float3(0.480f, -23.0f, 59.5f),
		float3(0.455f, -19.1f, 51.1f),
		float3(0.432f, -15.9f, 44.0f),
		float3(0.409f, -13.0f, 37.5f),
		float3(0.387f, -10.3f, 31.5f),
		float3(0.367f, -8.1f, 26.5f),
		float3(0.349f, -6.2f, 22.1f),
		float3(0.330f, -4.5f, 17.9f),
		float3(0.315f, -3.1f, 14.4f),
		float3(0.301f, -2.0f, 11.4f),
		float3(0.288f, -1.1f, 8.6f),
		float3(0.276f, -0.4f, 6.2f),
		float3(0.267f, 0.0f, 4.4f),
		float3(0.259f, 0.3f, 3.0f),
		float3(0.253f, 0.5f, 2.2f),
		float3(0.250f, 0.0f, 2.4f),
		float3(0.246f, -2.7f, 3.5f),
		float3(0.244f, -4.1f, 1.7f),
		float3(0.243f, -1.0f, -1.3f),
		float3(0.243f, 1.7f, -4.2f),
		float3(0.243f, 2.5f, -6.0f),
		float3(0.242f, 1.2f, -5.4f),
		float3(0.242f, -2.1f, -1.5f),
		float3(0.245f, -7.1f, 6.0f),
		float3(0.254f, -11.2f, 12.6f),
		float3(0.271f, -10.7f, 13.9f),
		float3(0.301f, -3.1f, 12.3f)
	};

	const float Hz_data[] = {
		20.0f,
		25.0f,
		31.5f,
		40.0f,
		50.0f,
		63.0f,
		80.0f,
		100.0f,
		125.0f,
		160.0f,
		200.0f,
		250.0f,
		315.0f,
		400.0f,
		500.0f,
		630.0f,
		800.0f,
		1000.0f,
		1250.0f,
		1600.0f,
		2000.0f,
		2500.0f,
		3150.0f,
		4000.0f,
		5000.0f,
		6300.0f,
		8000.0f,
		10000.0f,
		12500.0f
	};
	Lp = max(0.0f, log10(Lp) * 10.0f + 94.0f);
	int maxIdx = 29 - 1;
	int idx2 = 0;
	while (idx2 <= maxIdx && Hz_data[idx2] < hz)
		idx2++;

	int idx = max(idx2 - 1, 0);
	idx2 = min(idx2, maxIdx);
	float w = idx != idx2 ? (hz - Hz_data[idx]) / (Hz_data[idx2] - Hz_data[idx]) : 0;
	float Alpha_f = lerp(isofons[idx].x, isofons[idx2].x, w);
	float Lu = lerp(isofons[idx].y, isofons[idx2].y, w);
	float Tf = lerp(isofons[idx].z, isofons[idx2].z, w);
	//Convert dB to Fons
	float Bf = pow(0.4f * pow(10.0f, (Lp + Lu) * 0.1f - 9), Alpha_f) - pow(0.4f * pow(10.0f, (Tf + Lu) * 0.1f - 9), Alpha_f) + 0.005135f;
	//float Ln = 40.0f*Mathf.Log10 (Bf) + 94.0f;
	//convert Fons to dB
	//float Af = 0.00447f * (Mathf.Pow (10.0f, 0.025f * Ln) - 1.15f) + Mathf.Pow (0.4f * Mathf.Pow (10f, 0.1f * (Tf + Lu) - 9.0f), Alpha_f);
	//Af = Mathf.Max (Af, 0);
	//Lp = 10.0f / Alpha_f * Mathf.Log10 (Af) - Lu + 94.0f;

	//return Mathf.Pow (10.0f, (Ln - 94.0f)*0.1f);
	return Bf * Bf * Bf * Bf; //optimized calculatation (convert dB to raw values)
}
