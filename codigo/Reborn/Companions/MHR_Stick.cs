using System;
using System.Reflection;

namespace Companions;

public static class MHR_Stick
{
	private static readonly string[] Names = new string[4] { "GetJoyLeftStickX", "GetJoyLeftStickY", "GetJoyRightStickX", "GetJoyRightStickY" };

	private static MethodInfo[] s_methods;

	private static object[][] s_args;

	private static Func<float>[] s_fast;

	private static Type s_zinput;

	private static bool s_init;

	private static bool s_warned;

	public static float RX()
	{
		return Read(2);
	}

	public static float RY()
	{
		return Read(3);
	}

	public static float LX()
	{
		return Read(0);
	}

	public static float LY()
	{
		return Read(1);
	}

	private static float Read(int i)
	{
		if (!s_init)
		{
			Init();
		}
		if (s_fast[i] != null)
		{
			try
			{
				return s_fast[i]();
			}
			catch (Exception ex)
			{
				s_fast[i] = null;
				s_methods[i] = null;
				Warn(Names[i] + " failed: " + ex.Message);
				return 0f;
			}
		}
		MethodInfo methodInfo = s_methods[i];
		if (methodInfo == null)
		{
			return 0f;
		}
		try
		{
			object obj = null;
			if (!methodInfo.IsStatic)
			{
				obj = Instance();
				if (obj == null)
				{
					return 0f;
				}
			}
			return (float)methodInfo.Invoke(obj, s_args[i]);
		}
		catch (Exception ex2)
		{
			s_methods[i] = null;
			Warn(Names[i] + " failed: " + ex2.Message);
			return 0f;
		}
	}

	private static void Init()
	{
		s_init = true;
		s_methods = new MethodInfo[4];
		s_args = new object[4][];
		s_fast = new Func<float>[4];
		s_zinput = Type.GetType("ZInput, assembly_utils") ?? Type.GetType("ZInput, assembly_valheim");
		if (s_zinput == null)
		{
			Warn("ZInput type not found");
			return;
		}
		string text = "";
		for (int i = 0; i < 4; i++)
		{
			MethodInfo methodInfo = Find(Names[i], out var args);
			s_methods[i] = methodInfo;
			s_args[i] = args;
			if (methodInfo == null)
			{
				text = text + ((text.Length <= 0) ? "" : ", ") + Names[i];
			}
			else if (methodInfo.IsStatic && args.Length == 0)
			{
				try
				{
					s_fast[i] = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), methodInfo);
				}
				catch (Exception)
				{
					s_fast[i] = null;
				}
			}
		}
		if (text.Length > 0)
		{
			Warn("controller stick input not found in this game version (" + text + ")");
		}
	}

	private static void Warn(string msg)
	{
		if (!s_warned)
		{
			s_warned = true;
			if (CompanionsPlugin.Log != null)
			{
				CompanionsPlugin.Log.LogWarning("[Input] " + msg + " - the command wheel's stick input will read as centered");
			}
		}
	}

	private static MethodInfo Find(string name, out object[] args)
	{
		args = null;
		MethodInfo methodInfo = null;
		object[] array = null;
		MethodInfo[] methods = s_zinput.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (MethodInfo methodInfo2 in methods)
		{
			if (methodInfo2.Name != name || methodInfo2.ReturnType != typeof(float))
			{
				continue;
			}
			ParameterInfo[] parameters = methodInfo2.GetParameters();
			object[] array2 = new object[parameters.Length];
			bool flag = true;
			for (int j = 0; j < parameters.Length; j++)
			{
				if (parameters[j].ParameterType == typeof(bool))
				{
					array2[j] = false;
					continue;
				}
				if (parameters[j].IsOptional)
				{
					array2[j] = parameters[j].DefaultValue;
					continue;
				}
				flag = false;
				break;
			}
			if (flag && (methodInfo == null || parameters.Length < array.Length || (methodInfo2.IsStatic && !methodInfo.IsStatic && parameters.Length == array.Length)))
			{
				methodInfo = methodInfo2;
				array = array2;
			}
		}
		args = array;
		return methodInfo;
	}

	private static object Instance()
	{
		PropertyInfo property = s_zinput.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (property != null)
		{
			return property.GetValue(null, null);
		}
		FieldInfo fieldInfo = s_zinput.GetField("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? s_zinput.GetField("m_instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (fieldInfo != null)
		{
			return fieldInfo.GetValue(null);
		}
		return null;
	}
}
