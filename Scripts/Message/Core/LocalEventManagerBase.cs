using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// �ֲ��¼�������ʵ��
/// </summary>
public class LocalEventManager
{
    // �ֲ��¼��洢�ṹ��ʵ�� -> (�¼����� -> �������б�)
    private readonly Dictionary<object, Dictionary<string, List<Action<EventDefinition>>>> _localEvents =
        new Dictionary<object, Dictionary<string, List<Action<EventDefinition>>>>();

    // �Ƴ�������ش��룬��Ϊ�������캯��
    public LocalEventManager()
    {
        Initialize();
    }

    private void Initialize()
    {
        // ȷ���¼������ѳ�ʼ��
        EventDefinitionCache.Initialize();
        Debug.Log("�ֲ��¼���������ʼ�����");
    }

    /// <summary>
    /// Ϊָ��ʵ������¼�������
    /// </summary>
    public void AddListener<TEvt>(object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        if (instance == null)
        {
            Debug.LogError("��Ӿֲ��¼�ʧ�ܣ�ʵ������Ϊnull");
            return;
        }

        var eventInstance = new TEvt();
        var eventName = eventInstance.EventName;

        if (!EventDefinitionCache.TryGetEventType(eventName, out var eventType) || eventType != typeof(TEvt))
        {
            Debug.LogError($"��Ӿֲ��¼�ʧ�ܣ��¼� '{eventName}' δע������Ͳ�ƥ��");
            return;
        }

        if (eventInstance.Scope != EventScope.Instance)
        {
            Debug.LogError($"��Ӿֲ��¼�ʧ�ܣ��¼� '{eventName}' ���Ǿֲ��¼�");
            return;
        }

        Action<EventDefinition> baseListener = args => listener((TEvt)args);

        if (!_localEvents.ContainsKey(instance))
        {
            _localEvents[instance] = new Dictionary<string, List<Action<EventDefinition>>>();
        }

        var instanceEventDict = _localEvents[instance];
        if (!instanceEventDict.ContainsKey(eventName))
        {
            instanceEventDict[eventName] = new List<Action<EventDefinition>>();
        }

        if (!instanceEventDict[eventName].Contains(baseListener))
        {
            instanceEventDict[eventName].Add(baseListener);
        }
    }

    /// <summary>
    /// ��ָ��ʵ���Ƴ��¼�������
    /// </summary>
    public void RemoveListener<TEvt>(object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        if (instance == null) return;

        var eventInstance = new TEvt();
        var eventName = eventInstance.EventName;

        Action<EventDefinition> baseListener = args => listener((TEvt)args);

        if (_localEvents.TryGetValue(instance, out var instanceEventDict) &&
            instanceEventDict.TryGetValue(eventName, out var listeners))
        {
            listeners.Remove(baseListener);

            if (listeners.Count == 0)
            {
                instanceEventDict.Remove(eventName);
                if (instanceEventDict.Count == 0)
                {
                    _localEvents.Remove(instance);
                }
            }
        }
    }

    /// <summary>
    /// �Ƴ�ָ��ʵ���������¼�������
    /// </summary>
    public void RemoveAllListeners(object instance)
    {
        if (instance != null && _localEvents.ContainsKey(instance))
        {
            _localEvents.Remove(instance);
        }
    }

    /// <summary>
    /// ��ָ��Ŀ��ʵ���㲥�ֲ��¼�
    /// </summary>
    public void Broadcast<TEvt>(object instance, TEvt eventData) where TEvt : EventDefinition
    {
        if (instance == null)
        {
            Debug.LogError("�㲥�ֲ��¼�ʧ�ܣ�ʵ������Ϊnull");
            return;
        }

        if (eventData == null)
        {
            Debug.LogError("�㲥�ֲ��¼�ʧ�ܣ��¼����ݲ���Ϊnull");
            return;
        }

        var eventName = eventData.EventName;

        if (!EventDefinitionCache.TryGetEventType(eventName, out var eventType) || eventType != typeof(TEvt))
        {
            Debug.LogError($"�㲥�ֲ��¼�ʧ�ܣ��¼� '{eventName}' δע������Ͳ�ƥ��");
            return;
        }

        if (eventData.Scope != EventScope.Instance)
        {
            Debug.LogError($"�㲥�ֲ��¼�ʧ�ܣ��¼� '{eventName}' ���Ǿֲ��¼�");
            return;
        }

        if (_localEvents.TryGetValue(instance, out var instanceEventDict) &&
            instanceEventDict.TryGetValue(eventName, out var listeners))
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
                    Debug.LogError($"ִ�оֲ��¼� '{eventName}' ʱ����: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }
}

/// <summary>
/// �ֲ��¼���չ�������贫����������ʵ����
/// </summary>
public static class LocalEventExtensions
{
    /// <summary>
    /// Ϊ��ǰʵ����Ӿֲ��¼�������
    /// </summary>
    public static void AddLocalListener<TEvt>(this object instance, LocalEventManager manager, Action<TEvt> listener)
        where TEvt : EventDefinition, new()
    {
        manager.AddListener(instance, listener);
    }

    /// <summary>
    /// Ϊ��ǰʵ���Ƴ��ֲ��¼�������
    /// </summary>
    public static void RemoveLocalListener<TEvt>(this object instance, LocalEventManager manager, Action<TEvt> listener)
        where TEvt : EventDefinition, new()
    {
        manager.RemoveListener(instance, listener);
    }

    /// <summary>
    /// �Ƴ���ǰʵ�������оֲ��¼�������
    /// </summary>
    public static void RemoveAllLocalListeners(this object instance, LocalEventManager manager)
    {
        manager.RemoveAllListeners(instance);
    }

    /// <summary>
    /// �ӵ�ǰʵ���㲥�ֲ��¼�
    /// </summary>
    public static void BroadcastLocalEvent<TEvt>(this object instance, LocalEventManager manager, TEvt eventData)
        where TEvt : EventDefinition
    {
        manager.Broadcast(instance, eventData);
    }
}

/*
// �¼����ࣨʾ��������ǰ���壩
public enum EventScope { Instance, Global }
public abstract class EventDefinition
{
    public abstract string EventName { get; }
    public abstract EventScope Scope { get; }
}

// �Զ����¼�ʾ������ҵ÷��¼�
public class PlayerScoreEvent : EventDefinition
{
    public int Score { get; set; } // �¼�Я��������
    public override string EventName => "PlayerScoreEvent";
    public override EventScope Scope => EventScope.Instance; // ʵ�����¼�
}

private LocalEventManager _eventManager;

void Awake()
{
    _eventManager = new LocalEventManager(); // ��ʼ��ʱ���Զ���ʼ���¼�����
}

// ��������ʾ��
public class UIManager
{
    public UIManager(LocalEventManager eventManager)
    {
        // ��ʽ1��ֱ�ӵ��ù���������ע��
        eventManager.AddListener<PlayerScoreEvent>(this, OnPlayerScoreChanged);

        // ��ʽ2��ʹ����չ����ע�ᣨ����ࣩ
        this.AddLocalListener(eventManager, OnPlayerScoreChanged);
    }

    // �¼�������
    private void OnPlayerScoreChanged(PlayerScoreEvent evt)
    {
        Debug.Log($"��ҵ÷ָ��£�{evt.Score}");
    }
}

public class PlayerController
{
    private LocalEventManager _eventManager;
    private UIManager _uiManager; // ��Ҫ�����¼���ʵ��

    public PlayerController(LocalEventManager eventManager, UIManager uiManager)
    {
        _eventManager = eventManager;
        _uiManager = uiManager;
    }

    // �÷�ʱ�㲥�¼�
    public void AddScore(int score)
    {
        var eventData = new PlayerScoreEvent { Score = score };
        
        // ��ʽ1��ֱ�ӵ��ù������㲥
        _eventManager.Broadcast(_uiManager, eventData);

        // ��ʽ2��ʹ����չ�����㲥
        _uiManager.BroadcastLocalEvent(_eventManager, eventData);
    }
}

//�Ƴ�ָ���¼��ļ���
// ��ʽ1��ֱ�ӵ��ù���������
_eventManager.RemoveListener<PlayerScoreEvent>(this, OnPlayerScoreChanged);

// ��ʽ2��ʹ����չ����
this.RemoveLocalListener(_eventManager, OnPlayerScoreChanged);

//�Ƴ�ʵ���������¼�����
// ��ʽ1��ֱ�ӵ��ù���������
_eventManager.RemoveAllListeners(this);

// ��ʽ2��ʹ����չ����
this.RemoveAllLocalListeners(_eventManager);
 */