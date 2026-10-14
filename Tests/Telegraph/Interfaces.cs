using System.Collections.Generic;
using UnityEngine;
public enum CardPhysicalState { InHand, Offered }
public class Card3DInteractable : MonoBehaviour { public Card SourceCard; public Transform VisualRoot; public CardPhysicalState PhysicalState; }
public class HorizontalCardHolder : MonoBehaviour { public bool IsCardVisible(Card c)=>true; public List<Card> cards = new List<Card>(); }
public enum RelicType { DoubleDecay }
public class RelicSystem { public static RelicSystem Instance; public int GetRotThreshold(int n)=>n; public bool ShouldDecayThisTurn()=>true; public bool GetDecayAll()=>false; public bool IsDecayProtected(Card c,List<Card> h)=>false; public bool HasRelic(RelicType t)=>false; public bool GetDecayRotOnStack()=>false; public void OnDecayPhaseEnd(List<Card> h){} }

