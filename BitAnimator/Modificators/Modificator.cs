
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using System;
using System.Linq;
using UnityEngine;

namespace AudioVisualization.Modificators
{
    [Serializable]
    public abstract class Modificator : ScriptableObject, IModificator
    {
		public bool enabled = true;
		public bool Enabled { get { return enabled; } set { enabled = value; } }
		public virtual bool UseTempBuffer { get { return false; } }
		public virtual bool UseMask { get { return false; } }
		public virtual bool MultipassRequired { get { return false; } }
		public virtual ExecutionQueue Queue { get { return ExecutionQueue.Peaks; } }
		public virtual void Initialize(BitAnimator bitAnimator, BitAnimator.RecordSlot slot) { }
		public void InitializeSingleInstance<T>(BitAnimator bitAnimator, BitAnimator.RecordSlot slot) where T : Modificator
		{
			if(slot.modificators.FirstOrDefault(m => m.GetType() == typeof(T)) != default(T))
				throw new ModAlreadyExistException(this.GetType().Name + " mod alredy exist. this mod can't has multiple instances", this);
		}
#if UNITY_EDITOR
        public virtual void DrawProperty() { }
#endif
	}
	[Serializable]
	public class SerializedModificator
	{
		public string name;
		public string typeName;
		public string data;
		public SerializedModificator(Modificator mod)
		{
			Serialize(mod);
		}
		public Modificator Deserialize(Modificator mod)
		{
			if(mod == null)
				mod = ScriptableObject.CreateInstance(typeName) as Modificator;
			JsonUtility.FromJsonOverwrite(data, mod);
			mod.name = name;
			return mod;
		}
		public void Serialize(Modificator mod)
		{
			name = mod.name;
			typeName = mod.GetType().FullName;
			data = JsonUtility.ToJson(mod);
		}
	}
	public class ModAlreadyExistException : Exception
	{
		public Modificator context;
		public ModAlreadyExistException(string message = "", Modificator mod = null) : base(message) { context = mod; }
	}
	public class EngineNotSupportedException : Exception
	{
		public Modificator context;
		public EngineNotSupportedException(string message = "", Modificator mod = null) : base(message) { context = mod; }
	}
}

