using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// �¼�������ö�٣������壩
/// </summary>
public enum EventScope
{
    Global,   // ȫ���¼������ж����ܼ���
    Instance  // ʵ���¼������ض�����ʵ����Ӧ
}

/// <summary>
/// �¼�������ࣨ�����壩
/// </summary>
public abstract class EventDefinition
{
    /// <summary>
    /// �¼����ƣ�������붨�壩
    /// </summary>
    public abstract string EventName { get; }

    /// <summary>
    /// �¼�������������붨�壩
    /// </summary>
    public abstract EventScope Scope { get; }
}

/// <summary>
/// �¼����建�棨�������ࣩ
/// </summary>
internal static class EventDefinitionCache
{
    private static readonly Dictionary<string, Type> _allEventTypes = new Dictionary<string, Type>();
    private static bool _isInitialized;

    internal static void Initialize()
    {
        if (_isInitialized) return;

        DiscoverEventDefinitions();
        Debug.Log($"�¼����建���ʼ����ɣ������� {_allEventTypes.Count} ���¼�����");
        _isInitialized = true;
    }

    private static void DiscoverEventDefinitions()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsSubclassOf(typeof(EventDefinition)) && !type.IsAbstract)
                    {
                        var instance = Activator.CreateInstance(type) as EventDefinition;
                        if (instance != null && !_allEventTypes.ContainsKey(instance.EventName))
                        {
                            _allEventTypes.Add(instance.EventName, type);
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

    internal static bool TryGetEventType(string eventName, out Type type)
    {
        return _allEventTypes.TryGetValue(eventName, out type);
    }

    internal static bool ContainsEvent(string eventName)
    {
        return _allEventTypes.ContainsKey(eventName);
    }

    internal static IEnumerable<string> GetAllEventNames()
    {
        return _allEventTypes.Keys;
    }
}

/// <summary>
/// ȫ���¼���������������
/// </summary>
public class GlobalEventManager
{
    // ����ʵ��
    private static GlobalEventManager _instance;
    private static readonly object _lock = new object();

    // ȫ���¼��ֵ䣺�¼����� -> �������б�
    private readonly Dictionary<string, List<Action<EventDefinition>>> _globalEvents =
        new Dictionary<string, List<Action<EventDefinition>>>();

    public static GlobalEventManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new GlobalEventManager();
                        _instance.Initialize();
                    }
                }
            }
            return _instance;
        }
    }

    private GlobalEventManager() { }

    private void Initialize()
    {
        EventDefinitionCache.Initialize();
        Debug.Log("ȫ���¼���������ʼ�����");
    }

    /// <summary>
    /// ���ȫ���¼�����
    /// </summary>
    public void AddListener<TEvt>(Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        var eventInstance = new TEvt();
        var eventName = eventInstance.EventName;

        if (!EventDefinitionCache.TryGetEventType(eventName, out var eventType) || eventType != typeof(TEvt))
        {
            Debug.LogError($"���ȫ���¼�ʧ�ܣ��¼� '{eventName}' δ��������Ͳ�ƥ��");
            return;
        }

        if (eventInstance.Scope != EventScope.Global)
        {
            Debug.LogError($"���ȫ���¼�ʧ�ܣ��¼� '{eventName}' ����ȫ���¼�");
            return;
        }

        Action<EventDefinition> baseListener = args => listener((TEvt)args);

        if (!_globalEvents.ContainsKey(eventName))
        {
            _globalEvents[eventName] = new List<Action<EventDefinition>>();
        }

        if (!_globalEvents[eventName].Contains(baseListener))
        {
            _globalEvents[eventName].Add(baseListener);
        }
    }

    /// <summary>
    /// �Ƴ�ȫ���¼�����
    /// </summary>
    public void RemoveListener<TEvt>(Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        var eventInstance = new TEvt();
        var eventName = eventInstance.EventName;

        Action<EventDefinition> baseListener = args => listener((TEvt)args);

        if (_globalEvents.ContainsKey(eventName) && _globalEvents[eventName].Contains(baseListener))
        {
            _globalEvents[eventName].Remove(baseListener);

            if (_globalEvents[eventName].Count == 0)
            {
                _globalEvents.Remove(eventName);
            }
        }
    }

    /// <summary>
    /// �㲥ȫ���¼�
    /// </summary>
    public void Broadcast<TEvt>(TEvt eventData) where TEvt : EventDefinition
    {
        if (eventData == null)
        {
            Debug.LogError("�㲥ȫ���¼�ʧ�ܣ��¼����ݲ���Ϊnull");
            return;
        }

        var eventName = eventData.EventName;

        if (!EventDefinitionCache.TryGetEventType(eventName, out var eventType) || eventType != typeof(TEvt))
        {
            Debug.LogError($"�㲥ȫ���¼�ʧ�ܣ��¼� '{eventName}' δ��������Ͳ�ƥ��");
            return;
        }

        if (eventData.Scope != EventScope.Global)
        {
            Debug.LogError($"�㲥ȫ���¼�ʧ�ܣ��¼� '{eventName}' ����ȫ���¼�");
            return;
        }

        if (_globalEvents.TryGetValue(eventName, out var listeners))
        {
            var listenersCopy = new List<Action<EventDefinition>>(listeners);
            foreach (var listener in listenersCopy)
            {
                try
                {
                    listener?.Invoke(eventData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"����ȫ���¼� '{eventName}' ʱ����: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }

    /// <summary>
    /// ���ȫ���¼��Ƿ��Ѷ���
    /// </summary>
    public bool IsEventDefined(string eventName)
    {
        return EventDefinitionCache.ContainsEvent(eventName);
    }

    /// <summary>
    /// ��ȡ������ע����¼�����
    /// </summary>
    public IEnumerable<string> GetAllEventNames()
    {
        return EventDefinitionCache.GetAllEventNames();
    }
}

/// <summary>
/// ȫ���¼���չ��������ǿ�棩
/// </summary>
public static class GlobalEventExtensions
{
    // ԭ�е���չ�������ֲ���
    public static void AddGlobalListener<TEvt>(this Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        GlobalEventManager.Instance.AddListener(listener);
    }

    public static void RemoveGlobalListener<TEvt>(this Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        GlobalEventManager.Instance.RemoveListener(listener);
    }

    public static void BroadcastGlobalEvent<TEvt>(this TEvt eventData) where TEvt : EventDefinition
    {
        GlobalEventManager.Instance.Broadcast(eventData);
    }

    // ������չ������ֱ�ӽ��ܷ�����Ϊ������������ʽ����Action����
    public static void AddGlobalListener<TEvt>(this MonoBehaviour sender, Action<TEvt> listener)
        where TEvt : EventDefinition, new()
    {
        GlobalEventManager.Instance.AddListener(listener);
    }

    // ������չ�����������Ƴ�����
    public static void RemoveGlobalListener<TEvt>(this MonoBehaviour sender, Action<TEvt> listener)
        where TEvt : EventDefinition, new()
    {
        GlobalEventManager.Instance.RemoveListener(listener);
    }
}


/*
// ����һ��ȫ���¼�����ҵ÷ֱ仯�¼�
public class PlayerScoreChangedEvent : EventDefinition
{
    // �¼�Я��������
    public int NewScore { get; set; }
    public int OldScore { get; set; }

    // �¼����ƣ�����Ψһ��
    public override string EventName => "PlayerScoreChanged";

    // �¼�������ȫ���¼���
    public override EventScope Scope => EventScope.Global;
}

// �ٶ���һ��ʾ���¼�����������¼�
public class PlayerDeathEvent : EventDefinition
{
    public string PlayerName { get; set; }
    public int DeathReason { get; set; } // 0=���˻�ɱ, 1=����, 2=��ʱ

    public override string EventName => "PlayerDeath";
    public override EventScope Scope => EventScope.Global;
}

using UnityEngine;

public class UIScoreDisplay : MonoBehaviour
{
    private void OnEnable()
    {
        // ����1��ֱ��ͨ��������ע��
        GlobalEventManager.Instance.AddListener<PlayerScoreChangedEvent>(OnScoreChanged);

        // ����2��ʹ����չ����������ࣩ
        this.AddGlobalListener<PlayerDeathEvent>(OnPlayerDeath);
    }

    private void OnDisable()
    {
        // �Ƴ���������������ע���Ӧ�������ڴ�й©��
        GlobalEventManager.Instance.RemoveListener<PlayerScoreChangedEvent>(OnScoreChanged);
        this.RemoveGlobalListener<PlayerDeathEvent>(OnPlayerDeath);
    }

    // ����÷ֱ仯�¼�
    private void OnScoreChanged(PlayerScoreChangedEvent evt)
    {
        Debug.Log($"�����仯: �� {evt.OldScore} �� {evt.NewScore}");
        // ����UI��ʾ...
    }

    // ������������¼�
    private void OnPlayerDeath(PlayerDeathEvent evt)
    {
        Debug.Log($"{evt.PlayerName} ������ԭ��: {evt.DeathReason}");
        // ��ʾ�������...
    }
}

public class PlayerController : MonoBehaviour
{
    private int _currentScore;

    public void AddScore(int amount)
    {
        int oldScore = _currentScore;
        _currentScore += amount;

        // �����¼�ʵ������������
        var scoreEvent = new PlayerScoreChangedEvent
        {
            OldScore = oldScore,
            NewScore = _currentScore
        };

        // �㲥�¼������ַ�ʽ��
        // ��ʽ1��ͨ���������㲥
        GlobalEventManager.Instance.Broadcast(scoreEvent);

        // ��ʽ2��ʹ����չ����
        // scoreEvent.BroadcastGlobalEvent();
    }

    public void Die(int reason)
    {
        var deathEvent = new PlayerDeathEvent
        {
            PlayerName = "MainPlayer",
            DeathReason = reason
        };

        deathEvent.BroadcastGlobalEvent(); // ʹ����չ�����㲥
    }
}

// ����¼��Ƿ��Ѷ���
bool isDefined = GlobalEventManager.Instance.IsEventDefined("PlayerScoreChanged");

// ��ȡ�����Ѷ�����¼�����
IEnumerable<string> allEvents = GlobalEventManager.Instance.GetAllEventNames();
foreach (var eventName in allEvents)
{
    Debug.Log("�Ѷ����¼�: " + eventName);
}
 */