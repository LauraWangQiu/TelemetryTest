using System;
using System.IO;
using UnityEngine;

[Serializable]
public abstract class Event
{
    public string sessionId;
    public string gameId;
    public string eventType;
    public long timestamp;
    public string authKey;

    public Event(string sessionId, string gameId, string eventType)
    {
        this.sessionId = sessionId;
        this.gameId = gameId;
        this.eventType = eventType;
        this.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        this.authKey = ConfigManager.GetAuthKey();
    }

    public string ToJSON()
    {
        return JsonUtility.ToJson(this);
    }

    public virtual string ToCSV()
    {
        return $"{sessionId},{gameId},{eventType},{timestamp}";
    }

    // Otros formatos...

    public void WriteDataToFile(string path, Tracker.Format format)
    {
        string data;
        switch (format)
        {
            case Tracker.Format.JSON:
                data = ToJSON();
                break;
            case Tracker.Format.CSV:
                data = ToCSV();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
        System.IO.File.AppendAllText(path, data + "\n");
    }
}
