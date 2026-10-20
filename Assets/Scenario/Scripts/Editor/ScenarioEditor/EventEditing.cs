using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

public class EventCall
{
    public Component owner;
    public string eventName;
    public int index;
    public Object target;
    public string method;
    public PersistentListenerMode mode;
    public string stringArg;
    public bool boolArg;
    public Object objectArg;

    public string StringArgPath => $"{eventName}.m_PersistentCalls.m_Calls.Array.data[{index}].m_Arguments.m_StringArgument";

    public bool IsBroken
    {
        get
        {
            if (target == null || string.IsNullOrEmpty(method))
                return true;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetMethods(flags).Any(m => m.Name == method))
                    return false;
            }

            return true;
        }
    }

    public EventAction ToAction()
    {
        return new EventAction { target = target, method = method, mode = mode, stringArg = stringArg, boolArg = boolArg, objectArg = objectArg };
    }
}

public struct EventAction
{
    public Object target;
    public string method;
    public PersistentListenerMode mode;
    public string stringArg;
    public bool boolArg;
    public Object objectArg;

    public static EventAction Call(Object target, string method)
    {
        return new EventAction { target = target, method = method, mode = PersistentListenerMode.Void };
    }

    public static EventAction WithString(Object target, string method, string value)
    {
        return new EventAction { target = target, method = method, mode = PersistentListenerMode.String, stringArg = value };
    }

    public static EventAction WithBool(Object target, string method, bool value)
    {
        return new EventAction { target = target, method = method, mode = PersistentListenerMode.Bool, boolArg = value };
    }

    public static EventAction WithObject(Object target, string method, Object value)
    {
        return new EventAction { target = target, method = method, mode = PersistentListenerMode.Object, objectArg = value };
    }
}

// Правит постоянные подписки UnityEvent через SerializedObject, поэтому все изменения попадают в Undo
public static class EventEditing
{
    public static void ReadAll(Component owner, List<EventCall> result)
    {
        var so = new SerializedObject(owner);
        var property = so.GetIterator();
        var enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            var calls = property.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls == null || !calls.isArray)
                continue;

            for (var i = 0; i < calls.arraySize; i++)
            {
                var call = calls.GetArrayElementAtIndex(i);
                var args = call.FindPropertyRelative("m_Arguments");
                result.Add(new EventCall
                {
                    owner = owner,
                    eventName = property.name,
                    index = i,
                    target = call.FindPropertyRelative("m_Target").objectReferenceValue,
                    method = call.FindPropertyRelative("m_MethodName").stringValue,
                    mode = (PersistentListenerMode)call.FindPropertyRelative("m_Mode").intValue,
                    stringArg = args.FindPropertyRelative("m_StringArgument").stringValue,
                    boolArg = args.FindPropertyRelative("m_BoolArgument").boolValue,
                    objectArg = args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue
                });
            }
        }
    }

    public static List<EventAction> Read(Component owner, string eventName)
    {
        var calls = new List<EventCall>();
        ReadAll(owner, calls);
        return calls.Where(c => c.eventName == eventName).Select(c => c.ToAction()).ToList();
    }

    public static void Add(Component owner, string eventName, EventAction action)
    {
        var so = new SerializedObject(owner);
        var calls = FindCalls(so, eventName);
        calls.arraySize++;
        Write(calls.GetArrayElementAtIndex(calls.arraySize - 1), action);
        so.ApplyModifiedProperties();
    }

    public static void Replace(EventCall call, EventAction action)
    {
        var so = new SerializedObject(call.owner);
        Write(FindCalls(so, call.eventName).GetArrayElementAtIndex(call.index), action);
        so.ApplyModifiedProperties();
    }

    public static void SetString(EventCall call, string value)
    {
        var so = new SerializedObject(call.owner);
        so.FindProperty(call.StringArgPath).stringValue = value;
        so.ApplyModifiedProperties();
    }

    public static void Clear(Component owner, string eventName)
    {
        var so = new SerializedObject(owner);
        FindCalls(so, eventName).ClearArray();
        so.ApplyModifiedProperties();
    }

    public static void Remove(EventCall call)
    {
        RemoveAll(new[] { call });
    }

    // С конца, чтобы индексы оставшихся подписок не сдвигались
    public static void RemoveAll(IEnumerable<EventCall> calls)
    {
        foreach (var call in calls.Where(c => c.owner != null).OrderByDescending(c => c.index).ToList())
        {
            var so = new SerializedObject(call.owner);
            FindCalls(so, call.eventName).DeleteArrayElementAtIndex(call.index);
            so.ApplyModifiedProperties();
        }
    }

    private static SerializedProperty FindCalls(SerializedObject so, string eventName)
    {
        return so.FindProperty(eventName + ".m_PersistentCalls.m_Calls");
    }

    private static void Write(SerializedProperty call, EventAction action)
    {
        call.FindPropertyRelative("m_Target").objectReferenceValue = action.target;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = TypeName(action.target.GetType());
        call.FindPropertyRelative("m_MethodName").stringValue = action.method;
        call.FindPropertyRelative("m_Mode").intValue = (int)action.mode;
        call.FindPropertyRelative("m_CallState").intValue = (int)UnityEventCallState.RuntimeOnly;

        var args = call.FindPropertyRelative("m_Arguments");
        args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = action.objectArg;
        args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = action.objectArg != null ? TypeName(action.objectArg.GetType()) : "";
        args.FindPropertyRelative("m_IntArgument").intValue = 0;
        args.FindPropertyRelative("m_FloatArgument").floatValue = 0;
        args.FindPropertyRelative("m_StringArgument").stringValue = action.stringArg ?? "";
        args.FindPropertyRelative("m_BoolArgument").boolValue = action.boolArg;
    }

    // Тот же формат, что пишет инспектор Unity: «ScenarioStep, Assembly-CSharp», «UnityEngine.GameObject, UnityEngine»
    private static string TypeName(Type type)
    {
        var assembly = type.Assembly.GetName().Name;
        if (assembly.StartsWith("UnityEngine.") && assembly.EndsWith("Module"))
            assembly = "UnityEngine";
        return $"{type.FullName}, {assembly}";
    }
}
