using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// �¼�������ö��
/// </summary>
public enum EventScope
{
    Global,   // ȫ���¼������ж����ܼ���
    Instance  // ʵ���¼������ض�����ʵ����Ӧ
}

/// <summary>
/// �¼���������
/// </summary>
public abstract class EventArgsBase { }

/// <summary>
/// ����¼������������
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EventDefinitionAttribute : Attribute { }

/// <summary>
/// �����¼������࣬���¼���Ϣ��������Ͱ�
/// </summary>
/// <typeparam name="T">�¼��������ͣ�������EventArgsBase������</typeparam>
public class EventInfo<T> where T : EventArgsBase
{
    public string Name { get; }
    public EventScope Scope { get; }
    public Type ArgsType { get; }

    public EventInfo(string name, EventScope scope)
    {
        Name = name;
        Scope = scope;
        ArgsType = typeof(T);
    }
}

/// <summary>
/// �¼���������������MonoBehaviour�ĵ���ʵ��
/// </summary>
public class EventManager
{
    // ����ʵ��
    private static EventManager _instance;

    // �̰߳�ȫ�ĵ�����
    private static readonly object _lock = new object();

    // ȫ���¼��ֵ䣺�¼����� -> �¼��������б�
    private readonly Dictionary<string, List<Action<EventArgsBase>>> _globalEvents =
        new Dictionary<string, List<Action<EventArgsBase>>>();

    // ʵ���¼��ֵ䣺����ʵ�� -> (�¼����� -> �¼��������б�)
    private readonly Dictionary<object, Dictionary<string, List<Action<EventArgsBase>>>> _instanceEvents =
        new Dictionary<object, Dictionary<string, List<Action<EventArgsBase>>>>();

    // �����¼�����Ļ���
    private readonly Dictionary<string, EventInfo<EventArgsBase>> _allEvents =
        new Dictionary<string, EventInfo<EventArgsBase>>();

    /// <summary>
    /// ����ʵ��������
    /// </summary>
    public static EventManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new EventManager();
                        _instance.Initialize();
                    }
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// ˽�й��캯������ֹ�ⲿʵ����
    /// </summary>
    private EventManager() { }

    /// <summary>
    /// ��ʼ���¼����������Զ����������¼�����
    /// </summary>
    private void Initialize()
    {
        DiscoverEventDefinitions();
        Debug.Log($"�¼���������ʼ����ɣ������� {_allEvents.Count} ���¼�����");
    }

    /// <summary>
    /// �Զ����ֲ�ע�����б����EventDefinitionAttribute���¼�������
    /// </summary>
    private void DiscoverEventDefinitions()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    // ������Ƿ������¼���������
                    if (Attribute.IsDefined(type, typeof(EventDefinitionAttribute)))
                    {
                        // �����ȡ���о�̬�ֶΣ��¼����壩
                        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
                        foreach (var field in fields)
                        {
                            // ����ֶ������Ƿ�Ϊ����EventInfo<>
                            if (field.FieldType.IsGenericType &&
                                field.FieldType.GetGenericTypeDefinition() == typeof(EventInfo<>))
                            {
                                var eventInfo = field.GetValue(null) as EventInfo<EventArgsBase>;
                                if (eventInfo != null && !_allEvents.ContainsKey(eventInfo.Name))
                                {
                                    _allEvents.Add(eventInfo.Name, eventInfo);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"�����ȡ�¼�����ʱ����: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// ���ȫ���¼����������Ͱ汾��
    /// </summary>
    public void AddGlobalListener<T>(EventInfo<T> eventInfo, Action<T> listener) where T : EventArgsBase
    {
        if (eventInfo == null)
        {
            Debug.LogError("���ȫ���¼�����ʧ�ܣ��¼���Ϣ����Ϊnull");
            return;
        }

        if (!_allEvents.ContainsKey(eventInfo.Name))
        {
            Debug.LogError($"���ȫ���¼�����ʧ�ܣ��¼� '{eventInfo.Name}' δ����");
            return;
        }

        if (_allEvents[eventInfo.Name].Scope != EventScope.Global)
        {
            Debug.LogError($"���ȫ���¼�����ʧ�ܣ��¼� '{eventInfo.Name}' ����ȫ���¼�");
            return;
        }

        // �����ͼ�����ת��Ϊ���������
        Action<EventArgsBase> baseListener = args => listener((T)args);

        if (!_globalEvents.ContainsKey(eventInfo.Name))
        {
            _globalEvents[eventInfo.Name] = new List<Action<EventArgsBase>>();
        }

        if (!_globalEvents[eventInfo.Name].Contains(baseListener))
        {
            _globalEvents[eventInfo.Name].Add(baseListener);
        }
    }

    /// <summary>
    /// �Ƴ�ȫ���¼����������Ͱ汾��
    /// </summary>
    public void RemoveGlobalListener<T>(EventInfo<T> eventInfo, Action<T> listener) where T : EventArgsBase
    {
        if (eventInfo == null) return;

        // ������Ӧ�Ļ��������
        Action<EventArgsBase> baseListener = args => listener((T)args);

        if (_globalEvents.ContainsKey(eventInfo.Name) && _globalEvents[eventInfo.Name].Contains(baseListener))
        {
            _globalEvents[eventInfo.Name].Remove(baseListener);

            if (_globalEvents[eventInfo.Name].Count == 0)
            {
                _globalEvents.Remove(eventInfo.Name);
            }
        }
    }

    /// <summary>
    /// ����ȫ���¼������Ͱ汾��
    /// </summary>
    public void TriggerGlobalEvent<T>(EventInfo<T> eventInfo, T args) where T : EventArgsBase
    {
        if (eventInfo == null)
        {
            Debug.LogError("����ȫ���¼�ʧ�ܣ��¼���Ϣ����Ϊnull");
            return;
        }

        if (!_allEvents.ContainsKey(eventInfo.Name))
        {
            Debug.LogError($"����ȫ���¼�ʧ�ܣ��¼� '{eventInfo.Name}' δ����");
            return;
        }

        var storedEventInfo = _allEvents[eventInfo.Name];

        if (storedEventInfo.Scope != EventScope.Global)
        {
            Debug.LogError($"����ȫ���¼�ʧ�ܣ��¼� '{eventInfo.Name}' ����ȫ���¼�");
            return;
        }

        if (args != null && args.GetType() != storedEventInfo.ArgsType)
        {
            Debug.LogError($"�����¼� '{eventInfo.Name}' ʧ�ܣ��������Ͳ�ƥ�䣬Ԥ�� {storedEventInfo.ArgsType.Name}��ʵ�� {args.GetType().Name}");
            return;
        }

        if (_globalEvents.TryGetValue(eventInfo.Name, out var listeners))
        {
            var listenersCopy = new List<Action<EventArgsBase>>(listeners);
            foreach (var listener in listenersCopy)
            {
                try
                {
                    listener?.Invoke(args);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"�����¼� '{eventInfo.Name}' ʱ����: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }

    /// <summary>
    /// ���ʵ���¼����������Ͱ汾��
    /// </summary>
    public void AddInstanceListener<T>(object instance, EventInfo<T> eventInfo, Action<T> listener) where T : EventArgsBase
    {
        if (instance == null)
        {
            Debug.LogError("���ʵ���¼�����ʧ�ܣ�ʵ������Ϊnull");
            return;
        }

        if (eventInfo == null)
        {
            Debug.LogError("���ʵ���¼�����ʧ�ܣ��¼���Ϣ����Ϊnull");
            return;
        }

        if (!_allEvents.ContainsKey(eventInfo.Name))
        {
            Debug.LogError($"���ʵ���¼�����ʧ�ܣ��¼� '{eventInfo.Name}' δ����");
            return;
        }

        if (_allEvents[eventInfo.Name].Scope != EventScope.Instance)
        {
            Debug.LogError($"���ʵ���¼�����ʧ�ܣ��¼� '{eventInfo.Name}' ����ʵ���¼�");
            return;
        }

        // �����ͼ�����ת��Ϊ���������
        Action<EventArgsBase> baseListener = args => listener((T)args);

        // ȷ��ʵ�����ֵ��д���
        if (!_instanceEvents.ContainsKey(instance))
        {
            _instanceEvents[instance] = new Dictionary<string, List<Action<EventArgsBase>>>();
        }

        var instanceEventDict = _instanceEvents[instance];

        // ȷ���¼�������ʵ�����¼��ֵ��д���
        if (!instanceEventDict.ContainsKey(eventInfo.Name))
        {
            instanceEventDict[eventInfo.Name] = new List<Action<EventArgsBase>>();
        }

        // ��������ظ��ļ�����
        if (!instanceEventDict[eventInfo.Name].Contains(baseListener))
        {
            instanceEventDict[eventInfo.Name].Add(baseListener);
        }
    }

    /// <summary>
    /// �Ƴ�ʵ���¼����������Ͱ汾��
    /// </summary>
    public void RemoveInstanceListener<T>(object instance, EventInfo<T> eventInfo, Action<T> listener) where T : EventArgsBase
    {
        if (instance == null || eventInfo == null) return;

        // ������Ӧ�Ļ��������
        Action<EventArgsBase> baseListener = args => listener((T)args);

        if (_instanceEvents.TryGetValue(instance, out var instanceEventDict))
        {
            if (instanceEventDict.TryGetValue(eventInfo.Name, out var listeners))
            {
                listeners.Remove(baseListener);

                // ������б�
                if (listeners.Count == 0)
                {
                    instanceEventDict.Remove(eventInfo.Name);

                    // ���ʵ��û���κ��¼������ˣ����ֵ����Ƴ���ʵ��
                    if (instanceEventDict.Count == 0)
                    {
                        _instanceEvents.Remove(instance);
                    }
                }
            }
        }
    }

    /// <summary>
    /// �Ƴ�ʵ���������¼�����
    /// </summary>
    public void RemoveAllInstanceListeners(object instance)
    {
        if (instance != null && _instanceEvents.ContainsKey(instance))
        {
            _instanceEvents.Remove(instance);
        }
    }

    /// <summary>
    /// ����ʵ���¼������Ͱ汾��
    /// </summary>
    public void TriggerInstanceEvent<T>(object instance, EventInfo<T> eventInfo, T args) where T : EventArgsBase
    {
        if (instance == null)
        {
            Debug.LogError("����ʵ���¼�ʧ�ܣ�ʵ������Ϊnull");
            return;
        }

        if (eventInfo == null)
        {
            Debug.LogError("����ʵ���¼�ʧ�ܣ��¼���Ϣ����Ϊnull");
            return;
        }

        if (!_allEvents.ContainsKey(eventInfo.Name))
        {
            Debug.LogError($"����ʵ���¼�ʧ�ܣ��¼� '{eventInfo.Name}' δ����");
            return;
        }

        var storedEventInfo = _allEvents[eventInfo.Name];

        if (storedEventInfo.Scope != EventScope.Instance)
        {
            Debug.LogError($"����ʵ���¼�ʧ�ܣ��¼� '{eventInfo.Name}' ����ʵ���¼�");
            return;
        }

        if (args != null && args.GetType() != storedEventInfo.ArgsType)
        {
            Debug.LogError($"�����¼� '{eventInfo.Name}' ʧ�ܣ��������Ͳ�ƥ�䣬Ԥ�� {storedEventInfo.ArgsType.Name}��ʵ�� {args.GetType().Name}");
            return;
        }

        if (_instanceEvents.TryGetValue(instance, out var instanceEventDict) &&
            instanceEventDict.TryGetValue(eventInfo.Name, out var listeners))
        {
            var listenersCopy = new List<Action<EventArgsBase>>(listeners);
            foreach (var listener in listenersCopy)
            {
                try
                {
                    listener?.Invoke(args);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"����ʵ���¼� '{eventInfo.Name}' ʱ����: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }

    /// <summary>
    /// ��ȡ�¼���Ϣ
    /// </summary>
    public EventInfo<EventArgsBase> GetEventInfo(string eventName)
    {
        _allEvents.TryGetValue(eventName, out var eventInfo);
        return eventInfo;
    }

    /// <summary>
    /// ����¼��Ƿ��Ѷ���
    /// </summary>
    public bool IsEventDefined(string eventName)
    {
        return _allEvents.ContainsKey(eventName);
    }

    /// <summary>
    /// ��ȡ������ע����¼�����
    /// </summary>
    public IEnumerable<string> GetAllEventNames()
    {
        return _allEvents.Keys;
    }
}

/// <summary>
/// �¼�����������չ��������ʵ���¼���ʹ��
/// </summary>
public static class EventManagerExtensions
{
    /// <summary>
    /// Ϊ����ʵ������¼���������չ���������Ͱ汾��
    /// </summary>
    public static void AddInstanceListener<T>(this object instance, EventInfo<T> eventInfo, Action<T> listener) where T : EventArgsBase
    {
        EventManager.Instance.AddInstanceListener(instance, eventInfo, listener);
    }

    /// <summary>
    /// �Ƴ�����ʵ�����¼���������չ���������Ͱ汾��
    /// </summary>
    public static void RemoveInstanceListener<T>(this object instance, EventInfo<T> eventInfo, Action<T> listener) where T : EventArgsBase
    {
        EventManager.Instance.RemoveInstanceListener(instance, eventInfo, listener);
    }

    /// <summary>
    /// ��������ʵ�����¼�����չ���������Ͱ汾��
    /// </summary>
    public static void TriggerInstanceEvent<T>(this object instance, EventInfo<T> eventInfo, T args) where T : EventArgsBase
    {
        EventManager.Instance.TriggerInstanceEvent(instance, eventInfo, args);
    }
}
