using System;
using System.Collections.Generic;
using UnityEngine;

namespace DreamClass.QuestSystem
{
    public class QuestEventBus : SingletonCtrl<QuestEventBus>
    {
        private readonly Dictionary<string, Action<object>> eventTable = new();

        public void Subscribe(string eventName, Action<object> listener)
        {
            if (!eventTable.ContainsKey(eventName))
                eventTable[eventName] = delegate { };
            eventTable[eventName] += listener;
        }

        public void Unsubscribe(string eventName, Action<object> listener)
        {
            if (eventTable.ContainsKey(eventName))
                eventTable[eventName] -= listener;
        }

        public void Publish(string eventName, object context = null)
        {
            if (eventTable.ContainsKey(eventName))
                eventTable[eventName]?.Invoke(context);
        }
    }
}
