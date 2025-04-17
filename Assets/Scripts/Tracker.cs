using System.Collections.Concurrent;
using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;
using Firebase;
using System.Net.Http;
using System.Text;

public class Tracker
{
    public enum Format { JSON, CSV }
    public enum PersistenceType { LOCAL, DATABASE, WEBSERVER }

    private static Tracker _instance;
    public static Tracker Instance => _instance;

    private ConcurrentQueue<Event> eventQueue;
    private string route;
    private Format format;
    private PersistenceType persistenceType;
    private string sessionId;
    public string SessionId => sessionId;
    private int EVENTS_TO_WRITE_SIZE;

    private string webhookURL;

    public Tracker(string sessId)
    {
        sessionId = sessId;
        format = ConfigManager.GetFormat();
        persistenceType = ConfigManager.GetPersistenceType();
        eventQueue = new ConcurrentQueue<Event>();
        EVENTS_TO_WRITE_SIZE = ConfigManager.GetEventsToWriteSize();
        webhookURL = ConfigManager.GetWebhookURL();

        switch (this.persistenceType)
        {
            case PersistenceType.LOCAL:
                route = Application.dataPath + "/" + ConfigManager.GetLogFilename();
                Debug.Log("Local telemetry file path: " + route);
                break;
            case PersistenceType.DATABASE:
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
            case PersistenceType.WEBSERVER:
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
            switch (persistenceType)
            {
                case PersistenceType.LOCAL:
                    FlushQueueToFile();
                    break;
                case PersistenceType.DATABASE:
                    FlushQueueToFirebase();
                    break;
                case PersistenceType.WEBSERVER:
                    FlushQueueToWebServer();
                    break;
                // ...
            }
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

    private void FlushQueueToWebServer()
    {
        while (eventQueue.TryDequeue(out Event e))
        {
            SendEventToWebServer(e);
        }
    }

    public void SendEventToFirebase(Event e)
    {
        // Firebase solo permite envio de datos con formato JSON
        string json = e.ToJSON();

        Debug.Log("Sending event to Firebase: " + json);

        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        dbRef.Child("events")
            .Push()
            .SetRawJsonValueAsync(json)
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
                    Debug.Log("Event correctly sent to Firebase");
            });
    }

    private async void SendEventToWebServer(Event e)
    {
        string json = e.ToJSON();

        Debug.Log("Sending event to Google Sheets: " + json);

        using HttpClient client = new HttpClient();
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            HttpResponseMessage response = await client.PostAsync(webhookURL, content);

            if (response.IsSuccessStatusCode)
            {
                Debug.Log("Event correctly sent to Google Sheets");
            }
            else
            {
                Debug.LogError("Error sending event to Google Sheets: " + response.StatusCode);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Exception sending event to Google Sheets: " + ex.Message);
        }
    }
}
