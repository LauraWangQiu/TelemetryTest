using System;
using UnityEngine;

[Serializable]
public class ArrowShotEvent : Event
{
    public enum ArrowType { Damage, Teleport }
    public ArrowType arrowType;  // Tipo de flecha
    public Vector3 position;     // Posici�n desde donde se dispar�
    public bool hasHit;          // Si cumpli� su prop�sito

    public ArrowShotEvent(string sessionId, string gameId, ArrowType arrowType, Vector3 pos, bool hasHit)
        : base(sessionId, gameId, "ArrowShotEvent")
    {
        this.arrowType = arrowType;
        this.position = pos;
        this.hasHit = hasHit;
    }

    public override string ToCSV()
    {
        return base.ToCSV() + $",{arrowType},{position},{hasHit}";
    }
}
