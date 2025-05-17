
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

#define UNITY_PI 3.14159265358979323844f
#define M_E 2.71828182845904523536f

static const int TangentMode_FreeSmooth = 0; //inTangent == outTangent
static const int TangentMode_Broken = 1; //inTangent != outTangent
static const int TangentMode_LeftFree = 0;
static const int TangentMode_LeftConstant = 2; //inTangent == infinity
static const int TangentMode_LeftLinear = 4;
static const int TangentMode_LeftClampedAuto = 8;
static const int TangentMode_RightFree = 0;
static const int TangentMode_RightConstant = 32; //outTangent == infinity
static const int TangentMode_RightLinear = 64;
static const int TangentMode_RightClampedAuto = 128;
static const int TangentMode_Auto = TangentMode_LeftConstant | TangentMode_RightConstant;
static const int TangentMode_BothLinear = TangentMode_Broken | TangentMode_LeftLinear | TangentMode_RightLinear;
static const int TangentMode_BothConstant = TangentMode_Auto | TangentMode_BothLinear;
static const int TangentMode_ClampedAuto = TangentMode_LeftClampedAuto | TangentMode_RightClampedAuto;

static const int WeightedMode_None = 0;
static const int WeightedMode_In = 1;
static const int WeightedMode_Out = 2;
static const int WeightedMode_Both = 3;

struct Keyframe
{
	float time;
	float value;
	float inTangent;
	float outTangent;
	int tangentMode;
#ifdef UNITY_2018_1_OR_NEWER
	int weightedMode;
	float inWeight;
	float outWeight;
#endif
};
Buffer<float> _Input;
RWBuffer<float> _Output;
StructuredBuffer<Keyframe> _Keyframes;
RWStructuredBuffer<Keyframe> _OutputKeyframes;

groupshared Keyframe kf[MAX_WORKSIZE];
groupshared bool kf_mark[MAX_WORKSIZE];
groupshared float kf_rms[MAX_WORKSIZE];
RWBuffer<uint> _Keys_count; //lasts keyfrafes in groups
//ConsumeStructuredBuffer<Keyframe> InputKeyframes;
//AppendStructuredBuffer<Keyframe> _OutputKeyframes;

uniform float4 _MinimumValues;
uniform float4 _MaximumValues;
uniform uint3 _GridSize;
uniform uint _N;
uniform uint3 _GridOffset;
uniform uint _Source;
uniform float _ChunckTime;
uniform float _HalfWindowTime;

/*
//https://forum.unity.com/threads/need-way-to-evaluate-animationcurve-in-the-job.532149/
float Evaluate(in float iInterp, in float iLeft, in float vLeft, in float tLeft, in float iRight, in float vRight, in float tRight)
{
	float t = lerp(iLeft, iRight, iInterp);
	float4 scale = iRight - iLeft;
	scale.xy = 1;

	float4 parameters = float4(t * t * t, t * t, t, 1);
	float4x4 hermiteBasis = float4x4(
		2, -2, 1, 1,
		-3, 3, -2, -1,
		0, 0, 1, 0,
		1, 0, 0, 0
		);

	float4 control = float4(vLeft, vRight, tLeft, tRight) * scale;
	float4 basisWithParams = mul(parameters, hermiteBasis);
	float4 hermiteBlend = control * basisWithParams;
	return dot(hermiteBlend, 1);
}

inline float Evaluate(float t, Keyframe left, Keyframe right)
{
	return Evaluate(t, left.time, left.value, left.outTangent, right.time, right.value, right.inTangent);
}
*/
float EvaluateHermite(Keyframe left, Keyframe right, float time)
{
	float scale = right.time - left.time;
	float t = (time - left.time) / scale;
	float t2 = t * t;
	float t3 = t2 * t;

	float h10 = t3 - 2.0f * t2 + t;
	float h01 = 3.0f * t2 - 2.0f * t3;
	float h00 = 1.0f - h01;
	float h11 = t3 - t2;
	return h00 * left.value
		+ h10 * scale * left.outTangent
		+ h01 * right.value
		+ h11 * scale * right.inTangent;
}

#ifdef UNITY_2018_1_OR_NEWER
	float EvaluateBezier(Keyframe left, Keyframe right, float time)
	{
		float leftWeight = left.weightedMode & WeightedMode_Out ? left.outWeight : 1 / 3.0;
		float rightWeight = right.weightedMode & WeightedMode_In ? right.inWeight : 1 / 3.0;
		float scale = right.time - left.time;
		time = saturate((time - left.time) / scale);
		float t = time;
		{
			float P0 = 0;
			float P1 = leftWeight;
			float P2 = 1 - rightWeight;
			float P3 = 1;

			float a = -P0 + 3 * P1 - 3 * P2 + P3;
			float b = 3 * P0 - 6 * P1 + 3 * P2;
			float c = -3 * P0 + 3 * P1;
			float d = P0 - time;
			for (int i = 12; i; --i)
			{
				float t2 = t * t;
				float t3 = t2 * t;
				float fx = a * t3 + b * t2 + c * t + d;
				float dt = 3 * a * t2 + 2 * b * t + c;
				if (abs(dt) > 0.0001)
					t -= fx / dt;
			}
		}
		{
			float P0 = left.value;
			float P3 = right.value;
			float P1 = P0 + left.outTangent * leftWeight * scale;
			float P2 = P3 - right.inTangent * rightWeight * scale;

			float a = -P0 + 3 * P1 - 3 * P2 + P3;
			float b = 3 * P0 - 6 * P1 + 3 * P2;
			float c = -3 * P0 + 3 * P1;
			float d = P0;
			float t2 = t * t;
			float t3 = t2 * t;
			float y = a * t3 + b * t2 + c * t + d;
			return y;
		}
	}
	#define Evaluate(k0, k1, time) EvaluateBezier(k0, k1, time)
#else
	#define Evaluate(k0, k1, time) EvaluateHermite(k0, k1, time)
#endif

[numthreads(64, 1, 1)]
void FillKeyframes(uint3 id : SV_DispatchThreadID)
{
	if (id.x >= _GridSize.x)
		return;

	Keyframe k;
	k.time = id.x * _ChunckTime;
	k.value = _Input[id.x];
	k.inTangent = 0.0;
	k.outTangent = 0.0;
	k.tangentMode = TangentMode_FreeSmooth;
#ifdef UNITY_2018_1_OR_NEWER
	k.weightedMode = WeightedMode_None;
	k.inWeight = 1 / 3.0;
	k.outWeight = 1 / 3.0;
#endif
	_OutputKeyframes[id.x] = k;
}

inline float sqr(float x)
{
	return x * x;
}
[numthreads(MAX_WORKSIZE, 1, 1)]
void DecimateKeyframesKernel(uint3 id : SV_DispatchThreadID, uint localID : SV_GroupIndex)
{
	//Разделям задачу оптимизации между потоками
	//Рабочая группа по 1024 потока берет 1024 ключевых кадра
	/*if (id.x < _GridSize.x)
		kf[localID] = _Keyframes[id.x];
	else
		kf[localID] = (Keyframe)0;
	//
	float quality = _Scale;
	//Вычисляем значение на месте текущего ключа по соседним и записываем средне квадратичную разность между значением ключа и апроксимации по соседям
	kf_rms[localID] = sqr(Evaluate(kf[max(0, localID - 1)], kf[min(MAX_WORKSIZE - 1, localID + 1)], 0.5) - kf[localID].value);

	GroupMemoryBarrierWithGroupSync();
	float3 rms = float3(kf_rms[max(0, localID - 1)], kf_rms[localID], kf_rms[min(MAX_WORKSIZE - 1, localID + 1)]);

	if (rms.y < rms.x && rms.y < rms.z)
		kf_mark[localID] = rms.y > quality;
	else
		kf_mark[localID] = true;

	*/
	//Удаляем и записываем результат для правого ключа
	//float rms_R = sqr(Evaluate(kf[localID0], kf[localID + 3], 0.5) - kf[localID + 2].value);
	//Сравниваем какой вариант лучше: сохранить или удалить каждый из ключей

}

[numthreads(64, 1, 1)]
void CurveFilter(uint3 id : SV_DispatchThreadID, uint localID : SV_GroupIndex)
{
	//_GridSize.x - input values count
	//_N - keyframes count
	float time = id.x * _ChunckTime + _HalfWindowTime;

	Keyframe k0 = _Keyframes[0];
	Keyframe k1 = k0;
	for (uint i = 1; i < _N; i++)
	{
		k1 = _Keyframes[i];
		if (k1.time > time)
			break;
		k0 = k1;
	}
	float value;
	if (time >= k1.time)
		value = k1.value;
	else if (time <= k0.time)
		value = k0.value;
	else
		value = Evaluate(k0, k1, time);
	_Output[id.x] = max(_Output[id.x], value);
}

[numthreads(64, 1, 1)]
void MultiplyByCurve(uint3 id : SV_DispatchThreadID, uint localID : SV_GroupIndex)
{
	//_GridSize.x - input values count
	//_N - keyframes count
	float time = id.x * _ChunckTime + _HalfWindowTime;

	Keyframe k0 = _Keyframes[0];
	Keyframe k1 = k0;
	for (uint i = 1; i < _N; i++)
	{
		k1 = _Keyframes[i];
		if (k1.time > time)
			break;
		k0 = k1;
	}
	float value;
	if (time >= k1.time)
		value = k1.value;
	else if (time <= k0.time)
		value = k0.value;
	else
		value = Evaluate(k0, k1, time);
	_Output[id.x] *= value;
}
[numthreads(64, 1, 1)]
void RemapKernel(uint3 id : SV_DispatchThreadID, uint localID : SV_GroupIndex)
{
	if (id.x >= _GridSize.x)
		return;

	//_GridSize.x - input values count
	//_GridSize.z - keyframes count
	float value = saturate(_Input[id.x + _GridOffset.x]);

	Keyframe k0 = _Keyframes[0];
	Keyframe k1 = k0;
	for (uint i = 1; i < _GridSize.z; i++)
	{
		k1 = _Keyframes[i];
		if (k1.time > value)
			break;
		k0 = k1;
	}
	if (value >= k1.time)
		value = k1.value;
	else if (value <= k0.time)
		value = k0.value;
	else
		value = Evaluate(k0, k1, value);
	_Output[id.x] = value;
}

[numthreads(64, 1, 1)]
void KeyframesCreator(uint3 id : SV_DispatchThreadID, uint localID : SV_GroupIndex)
{
	if (id.x >= _GridSize.x)
		return;

	float value = _Source > 1 ? frac(_Input[id.x] * _Source) : saturate(_Input[id.x]);
	float min[4] = { _MinimumValues };
	float max[4] = { _MaximumValues };
	float time = id.x * _ChunckTime + _HalfWindowTime;
	uint totalChannels = _N;
	for (uint channel = 0; channel < totalChannels; channel++)
	{
		Keyframe result;
		result.time = time;
		result.value = lerp(min[channel], max[channel], value);
		result.inTangent = 0;
		result.outTangent = 0;
		result.tangentMode = TangentMode_Broken;
#ifdef UNITY_2018_1_OR_NEWER
		result.weightedMode = WeightedMode_None;
		result.inWeight = 1 / 3.0;
		result.outWeight = 1 / 3.0;
#endif
		_OutputKeyframes[id.x + channel * _GridSize.x] = result;
	}

}
[numthreads(64, 1, 1)]
void RemapGradientKernel(uint3 id : SV_DispatchThreadID, uint localID : SV_GroupIndex)
{
	if (id.x >= _GridSize.x)
		return;

	//_GridSize.x - input values count
	//_GridSize.y - channels output
	//_GridSize.z - colorkeyframes count
	//_N - alpha keyframes gradient count
	//_Source - loops count the gradient
	float value = _Source > 1 ? frac(_Input[id.x] * _Source) : saturate(_Input[id.x]);
	float time = id.x * _ChunckTime + _HalfWindowTime;
	for (uint channel = 0; channel < _GridSize.y; channel++)
	{
		Keyframe k0 = _Keyframes[channel * _GridSize.z];
		Keyframe k1 = k0;
		uint keysCount = channel == 3 ? _N : _GridSize.z;
		for (uint i = 1; i < keysCount; i++)
		{
			k1 = _Keyframes[channel * _GridSize.z + i];
			if (k1.time > value)
				break;
			k0 = k1;
		}
		Keyframe result;
		result.time = time;
		if (value >= k1.time)
			result.value = k1.value;
		else if (value <= k0.time)
			result.value = k0.value;
		else
			result.value = lerp(k0.value, k1.value, saturate((value - k0.time) / (k1.time - k0.time)));
		result.inTangent = 0;
		result.outTangent = 0;
		result.tangentMode = TangentMode_Broken;
#ifdef UNITY_2018_1_OR_NEWER
		result.weightedMode = WeightedMode_None;
		result.inWeight = 1 / 3.0;
		result.outWeight = 1 / 3.0;
#endif
		_OutputKeyframes[id.x + channel * _GridSize.x] = result;
	}
}
[numthreads(64, 1, 1)]
void ConvertToQuaternions(uint3 id : SV_DispatchThreadID)
{
	// yaw (Z), pitch (Y), roll (X)
	float x = _OutputKeyframes[id.x + 0 * _GridSize.x].value * UNITY_PI / 180.0;
	float y = _OutputKeyframes[id.x + 1 * _GridSize.x].value * UNITY_PI / 180.0;
	float z = _OutputKeyframes[id.x + 2 * _GridSize.x].value * UNITY_PI / 180.0;
	// Abbreviations for the various angular functions
	float cy = cos(z * 0.5);
	float sy = sin(z * 0.5);
	float cp = cos(y * 0.5);
	float sp = sin(y * 0.5);
	float cr = cos(x * 0.5);
	float sr = sin(x * 0.5);

	float4 q;
	q.w = cy * cp * cr + sy * sp * sr;
	q.x = cy * cp * sr - sy * sp * cr;
	q.y = sy * cp * sr + cy * sp * cr;
	q.z = sy * cp * cr - cy * sp * sr;

	_OutputKeyframes[id.x + 0 * _GridSize.x].value = q.x;
	_OutputKeyframes[id.x + 1 * _GridSize.x].value = q.y;
	_OutputKeyframes[id.x + 2 * _GridSize.x].value = q.z;
	_OutputKeyframes[id.x + 3 * _GridSize.x].value = q.w;
}