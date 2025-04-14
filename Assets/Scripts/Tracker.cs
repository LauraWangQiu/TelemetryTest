using System.Collections.Concurrent;
using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;
using Firebase;

public class Tracker
{
    public enum Format { JSON, CSV }
    public enum PersistenceType { LOCAL, NETWORK }

    private static Tracker _instance;
    public static Tracker Instance => _instance;

    private ConcurrentQueue<Event> eventQueue;
    private string route;
    private Format format;
    private PersistenceType persistenceType;
    private string sessionId;
    public string SessionId => sessionId;
    public int EVENTS_TO_WRITE_SIZE;

    public Tracker(string sessionId)
    {
        this.sessionId = sessionId;
        this.format = ConfigManager.GetFormat();
        this.persistenceType = ConfigManager.GetPersistenceType();
        this.eventQueue = new ConcurrentQueue<Event>();
        this.EVENTS_TO_WRITE_SIZE = ConfigManager.GetEventsToWriteSize();

        switch (this.persistenceType)
        {
            case PersistenceType.LOCAL:
                route = Application.dataPath + "/" + ConfigManager.GetLogFilename();
                Debug.Log("Local telemetry file path: " + route);
                break;
            case PersistenceType.NETWORK:
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.Result == DependencyStatus.Available)
                    {
                        Debug.Log("Firebase available.");
                    }
                    else
                    {
                        Debug.LogError("Firebase not available: " + task.Result);
                    }
                });
                break;
            // ...
        }

        _instance = this;
    }

    public void SendEvent(Event e)
    {
        eventQueue.Enqueue(e);

        if (eventQueue.Count >= EVENTS_TO_WRITE_SIZE)
        {
            if (persistenceType == PersistenceType.LOCAL)
                FlushQueueToFile();
            else if (persistenceType == PersistenceType.NETWORK)
                FlushQueueToFirebase();
        }
    }

    private void FlushQueueToFile()
    {
        while (eventQueue.TryDequeue(out Event e))
        {
            e.WriteDataToFile(route, format);
        }
    }

    private void FlushQueueToFirebase()
    {
        while (eventQueue.TryDequeue(out Event e))
        {
            SendEventToFirebase(e);
        }
    }

    public void SendEventToFirebase(Event e)
    {
        // Firebase solo permite envio de datos con formato JSON
        Debug.Log("Sending event to Firebase: " + e.ToJSON());

        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        dbRef.Child("events")
            .Push()
            .SetRawJsonValueAsync(e.ToJSON())
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("Error sending event to Firebase");

                    if (task.Exception != null)
                    {
                        foreach (var innerException in task.Exception.InnerExceptions)
                        {
                            Debug.LogError("Firebase error: " + innerException.Message);
                        }
                    }
                }
                else if (task.IsCompleted)
                    Debug.Log("Event correctly sent");
            });
    }
}
